using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
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
        
        var routingAgent = CreateRoutingAgent(projectClient, configuration);
        
        var specializedAgents = new List<AIAgent>();
        specializedAgents.AddRange([
            routingAgent,
            await CreateRecommendationAgent(projectClient, configuration),
            await CreateWeatherAgent(projectClient, configuration), 
            await CreateHotelAgent(projectClient, configuration), 
            await CreateEventAgent(projectClient, configuration)]);

        var workflowBuilder = AgentWorkflowBuilder.CreateHandoffBuilderWith(routingAgent);

        foreach (var agent in specializedAgents)
        {
            workflowBuilder.WithHandoffs(specializedAgents.Except([agent]), agent);
        }

        var workflow = workflowBuilder
                       .EnableReturnToPrevious()
                       .Build()
                       .AsAIAgent();

        var session = await workflow.CreateSessionAsync();
        do
        {
            Console.Write("User: ");
            string userInput = Console.ReadLine() ?? string.Empty;

            if (userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var run = workflow.RunStreamingAsync(new ChatMessage(ChatRole.User, userInput), session);
            string? lastAgent = null;
            await foreach (var update in run)
            {
                string? currentAgent = update.AuthorName;
                if (currentAgent is not null && !currentAgent.Equals(lastAgent))
                {
                    Console.Write($"{currentAgent}: ");
                }
                
                Console.Write(update.Text);
                
                lastAgent = currentAgent;
            }

            lastAgent = null;
            Console.WriteLine();
        } while (true);
    }
    
    private static AIAgent CreateRoutingAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        return projectClient.ProjectOpenAIClient
                            .GetChatClient(configuration["modelDeployment"])
                            .AsIChatClient()
                            .AsAIAgent(
                                "Always handoff execution to: new-maf-agent-weather (for providing weather conditions on a given city), new-maf-agent-recommendation (for providing activity recommendations based on weather), new-maf-agent-hotel (for providing hotel information based on recommendations), new-maf-agent-event (for providing events information based on recommendations). You never answer questions yourself, you always handoff to the connected agents.",
                                "routing-agent");
    }
    
    private static async Task<AIAgent> CreateWeatherAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        return projectClient.ProjectOpenAIClient
                            .GetChatClient(configuration["modelDeployment"])
                            .AsIChatClient()
                            .AsAIAgent(
                                "You are an AI agent that provides weather information. When asked about the weather in any city, ALWAYS immediately call the WeatherFunction tool with the city name. Never ask for confirmation or more information — just call the tool.",
                                "weather-agent",
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

        return projectClient.ProjectOpenAIClient
                            .GetChatClient(configuration["modelDeployment"])
                            .AsIChatClient()
                            .AsAIAgent(
                                "You are an AI agent that provides recommendations of what to do in each city based on the current weather at a city. You always begin your answer with: Based on the current weather conditions in {city}, I recommend you to {activity}. You can use the web search tool to find more information about activities.",
                                "recommendation-agent");
    }
    
    // Foundry client/agents does not turn back handoff, they can only be handoff to but do not return control. As a workaround they can be exposed as tools to a wrapper local agent.
    private static async Task<AIAgent> CreateHotelAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var hotelSearchTool = projectClient.AsAIAgent(
            configuration["modelDeployment"],
            "You are an AI agent that provides hotel information based on recommendations given, if the user requests for it. You use the web search tool to find hotel information.",
            "hotel-agent",
            tools: [new HostedWebSearchTool()]).AsAIFunction();
        
        return projectClient.ProjectOpenAIClient
                            .GetChatClient(configuration["modelDeployment"])
                            .AsIChatClient()
                            .AsAIAgent(
                                "You are an AI agent that provides hotel information based on recommendations given, if the user requests for it. You use the web search tool to find hotel information.",
                                "hotel-agent",
                                tools: [hotelSearchTool]);
    }
    
    // Foundry client/agents does not turn back handoff, they can only be handoff to but do not return control. As a workaround they can be exposed as tools to a wrapper local agent.
    private static async Task<AIAgent> CreateEventAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var eventSearchTool = projectClient.AsAIAgent(
            configuration["modelDeployment"],
            "You are an AI agent that provides events information based on recommendations given, if the user requests for it. You use the web search tool to find events information.",
            "event-agent",
            tools: [new HostedWebSearchTool()]).AsAIFunction();
        
        return projectClient.ProjectOpenAIClient
                            .GetChatClient(configuration["modelDeployment"])
                            .AsIChatClient()
                            .AsAIAgent(
                                "You are an AI agent that provides events information based on recommendations given, if the user requests for it. You use the web search tool to find events information.",
                                "event-agent",
                                tools: [eventSearchTool]);
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