using System;
using System.Threading.Tasks;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

namespace AI_103.ChatAndAgents;

#pragma warning disable OPENAI001


public sealed class AgentWithAiSearch
{
    public static async Task Run(IConfiguration configuration)
    {
        var projectClient = new AIProjectClient(
            new Uri(configuration["ProjectUrl"]!),
            new AzureCliCredential());
        
        var agent = await CreateAgent(projectClient, configuration);

        ProjectConversation conversation = await projectClient.ProjectOpenAIClient.GetProjectConversationsClient()
                                                              .CreateProjectConversationAsync();

        var responsesClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgent(agent.Name, conversation.Id);

        while (true)
        {
            Console.Write("User: ");
            
            string? userMessage = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(userMessage)) break;

            ResponseResult response = await responsesClient.CreateResponseAsync(userMessage);
            
            Console.WriteLine($"Assistant: {response.GetOutputText()}");
            Console.WriteLine();
        }
    }

    private static async Task<ProjectsAgentVersion> CreateAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var agentDefinition = new DeclarativeAgentDefinition(configuration["ModelDeployment"])
        {
            Instructions = "You're an assistant that answer all question based on the connected Azure AI Search tool.",
        };
        
        var searchTool = new AzureAISearchTool(new AzureAISearchToolOptions([new AzureAISearchToolIndex
        {
            IndexName = configuration["AISearchIndexName"],
            QueryType = AzureAISearchQueryType.VectorSemanticHybrid,
            TopK = 5,
            ProjectConnectionId = configuration["AISearchProjectConnection"]
        }]));
        
        agentDefinition.Tools.Add(searchTool);

        ProjectsAgentVersion agent = await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            "new-docs-agent",
            options: new(agentDefinition));

        return agent;
    }
}