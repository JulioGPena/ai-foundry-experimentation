using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

namespace AI_103.ChatAndAgents;

#pragma warning disable OPENAI001

public sealed class AgentFrameworkWorkflow
{
    private static HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("https://api.openweathermap.org")
    };

    private static string? _apiKey;
    
    public static async Task Run(IConfiguration configuration)
    {
        _apiKey = configuration["WeatherApiKey"];
        
        var projectClient = new AIProjectClient(
            new Uri(configuration["ProjectUrl"]!),
            new AzureCliCredential());
        
        var routingAgent = await CreateRoutingAgent(projectClient, configuration);
        var weatherAgent = await CreateWeatherAgent(projectClient, configuration);
        var recommendationAgent = await CreateRecommendationAgent(projectClient, configuration);
        var hotelAgent = await CreateHotelAgent(projectClient, configuration);
        var eventAgent = await CreateEventAgent(projectClient, configuration);

        var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(routingAgent)
                                                .WithHandoffs(routingAgent,
                                                    [weatherAgent, recommendationAgent, hotelAgent, eventAgent])
                                                .WithHandoffs(
                                                    [weatherAgent, recommendationAgent, hotelAgent, eventAgent],
                                                    routingAgent)
                                                .EnableReturnToPrevious()
                                                .Build()
                                                .AsAIAgent();

        var session = await workflow.CreateSessionAsync();
        while (true)
        {
            Console.Write("User: ");
            
            string? userMessage = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(userMessage)) break;
            
            // Tried a couple of different ways but don't seem that Foundry agents
            // supports handoff orchestration at this given stage.
            // The 'connected' agents never get executed (verified by traces in Portal)
            // and got only some weird responses indicating that the routing agent is trying
            // to handoff execution, but it's never 'picked up', example:
            // User: how is the weather in Lisbon?
            // Assistant: HANDOFF: new-maf-agent-weather
            //
            // Request:
            // - City: Lisbon
            //
            // Please provide the current weather conditions for Lisbon.
            //
            // User: 
            //
            // 
            var response = await workflow.RunAsync(userMessage, session);
            
            Console.WriteLine($"Assistant: {response.Text}");
            Console.WriteLine();
        }
    }
    
    private static async Task<AIAgent> CreateWeatherAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var agentDefinition = new DeclarativeAgentDefinition(configuration["modelDeployment"])
        {
            Instructions = "You are an AI agent that provides weather information. When asked about the weather in any city, ALWAYS immediately call the WeatherFunction tool with the city name. Never ask for confirmation or more information — just call the tool.",
        };
        
        var schema = CreateSchemaForProperty("request", typeof(WeatherRequest));
        
        agentDefinition.Tools.Add(new FunctionTool(
            "WeatherFunction", 
            BinaryData.FromString(schema.ToJsonString()),
            null));
        
        ProjectsAgentVersion agent = await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            "new-maf-agent-weather",
            options: new(agentDefinition));

        return projectClient.AsAIAgent(
            agent,
            tools: [AIFunctionFactory.Create(WeatherFunction, "WeatherFunction")]);
    }
    
    private static async Task<AIAgent> CreateRecommendationAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var agentDefinition = new DeclarativeAgentDefinition(configuration["modelDeployment"])
        {
            Instructions = "You are an AI agent that provides recommendations of what to do in each city based on the current weather at a city. You always begin your answer with: Based on the current weather conditions in {city}, I recommend you to {activity}."
        };
        
        ProjectsAgentVersion agent = await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            "new-maf-agent-recommendation",
            options: new(agentDefinition));

        return projectClient.AsAIAgent(agent);
    }
    
    private static async Task<AIAgent> CreateRoutingAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var agentDefinition = new DeclarativeAgentDefinition(configuration["modelDeployment"])
        {
            Instructions = "Always handoff execution to: new-maf-agent-weather (for providing weather conditions on a given city), new-maf-agent-recommendation (for providing activity recommendations based on weather), new-maf-agent-hotel (for providing hotel information based on recommendations), new-maf-agent-event (for providing events information based on recommendations). You never answer questions yourself, you always handoff to the connected agents."
        };
        
        ProjectsAgentVersion agent = await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            "new-maf-agent-routing",
            options: new(agentDefinition));

        return projectClient.AsAIAgent(agent);
    }
    
    private static async Task<AIAgent> CreateHotelAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var agentDefinition = new DeclarativeAgentDefinition(configuration["modelDeployment"])
        {
            Instructions = "You are an AI agent that provides hotel information based on recommendations given, if the user requests for it. You use the web search tool to find hotel information."
        };
        
        agentDefinition.Tools.Add(new WebSearchTool());
        
        ProjectsAgentVersion agent = await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            "new-maf-agent-hotel",
            options: new(agentDefinition));

        return projectClient.AsAIAgent(agent);
    }
    
    private static async Task<AIAgent> CreateEventAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var agentDefinition = new DeclarativeAgentDefinition(configuration["modelDeployment"])
        {
            Instructions = "You are an AI agent that provides events information based on recommendations given, if the user requests for it. You use the web search tool to find events information."
        };
        
        agentDefinition.Tools.Add(new WebSearchTool());
        
        ProjectsAgentVersion agent = await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            "new-maf-agent-event",
            options: new(agentDefinition));

        return projectClient.AsAIAgent(agent);
    }

    private static JsonObject CreateSchemaForProperty(string propertyName, Type propertyType)
    {
        // Schemas for local functions must match the function input (parameters).
        // Meaning that getting the schema for the input parameter type is not enough.
        // The schema must be constructed from the top level argument name.
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        var typeInfo = options.GetTypeInfo(propertyType);
        var innerSchema = typeInfo.GetJsonSchemaAsNode();

        if (innerSchema is JsonObject innerObj)
        {
            innerObj["type"] = "object";
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                [propertyName] = innerSchema
            },
            ["required"] = new JsonArray(propertyName)
        };
    }
    
    [Description("Provides weather information for a given city.")]
    private static async Task<WeatherResponse?> WeatherFunction(WeatherRequest request)
    {
        var response = await _httpClient.GetAsync($"/data/2.5/weather?q={request.City}&appid={_apiKey}&units=metric");
        response.EnsureSuccessStatusCode();
        var weatherInfo = await response.Content.ReadFromJsonAsync<WeatherResponse>();  
        return weatherInfo;
    }

    private record WeatherRequest(string City);

    private record WeatherResponse
    {
        [JsonPropertyName("main")]
        public MainInformation? Main { get; init; }
        
        [JsonPropertyName("weather")]
        public WeatherDescription[]? Weather { get; init; }
        
        public record MainInformation
        {
            [JsonPropertyName("temp")]
            public float? Temp { get; init; }
        }
        public record WeatherDescription
        {
            [JsonPropertyName("main")]
            public string? Main { get; init; }
            
            [JsonPropertyName("description")]
            public string? Description { get; init; }
            
        }
    }
}