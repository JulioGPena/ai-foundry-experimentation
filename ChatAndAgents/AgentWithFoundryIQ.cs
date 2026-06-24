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

public sealed class AgentWithFoundryIQ
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
            
            ResponseResult? response = await responsesClient.CreateResponseAsync([ResponseItem.CreateUserMessageItem(userMessage)]);
            Console.WriteLine($"Assistant: {response?.GetOutputText()}");
            Console.WriteLine();
        }
    }
    
    private static async Task<ProjectsAgentVersion> CreateAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var agentDefinition = new DeclarativeAgentDefinition(configuration["ModelDeployment"])
        {
            Instructions = "You're an assistant that answer all question based on the connected knowledge source.",
        };
        
        // There is not first party way to add a knowledge base to an agent, right now this is done through an MCP Tool.
        // The connection id here is not the 'connection id' of the Azure AI Search connection but the 'name' of a connection id
        // used for Knowledge Base connections.
        // The way I was able to find that at this given stage was to create an agent at the portal with a connected knowledge
        // base and from the YAML agent definition get the knowledge base connection id.
        var mcpTool = new McpTool("knowledge-base", new Uri(configuration["KnowledgeBaseUrl"]!))
        {
            ProjectConnectionId = configuration["KnowledgeBaseConnectionId"]!,
            ToolCallApprovalPolicy = new McpToolCallApprovalPolicy(GlobalMcpToolCallApprovalPolicy.NeverRequireApproval)
        };
        
        agentDefinition.Tools.Add(mcpTool);

        ProjectsAgentVersion agent = await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            "new-foundry-iq-agent",
            options: new(agentDefinition));

        return agent;
    }
}