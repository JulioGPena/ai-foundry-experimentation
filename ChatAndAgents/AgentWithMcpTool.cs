using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

namespace AI_103.ChatAndAgents;

#pragma warning disable OPENAI001

public sealed class AgentWithMcpTool
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
            foreach (var responseItem in response.OutputItems)
            {
                if (responseItem is McpToolCallApprovalRequestItem toolCallApprovalRequest)
                {
                    Console.WriteLine($"Assistant: The agent is requesting to call tool '{toolCallApprovalRequest.ToolName}'. Approve (y/n)? ");
                    string? approvalInput = Console.ReadLine();
                
                    bool isApproved = approvalInput?.Equals("y", StringComparison.OrdinalIgnoreCase) ?? false;

                    response = await responsesClient.CreateResponseAsync([
                        ResponseItem.CreateMcpApprovalResponseItem(
                            toolCallApprovalRequest.Id,
                            isApproved)
                    ]);
                }
            }
            
            Console.WriteLine($"Assistant: {response?.GetOutputText()}");
            Console.WriteLine();
        }
    }

    private static async Task<ProjectsAgentVersion> CreateAgent(AIProjectClient projectClient, IConfiguration configuration)
    {
        var agentDefinition = new DeclarativeAgentDefinition(configuration["modelDeployment"])
        {
            Instructions = "You are an AI agent that answer question based on the connected MCP 'api-specs'."
        };

        var mcpTool = new McpTool("api-specs", new Uri("https://learn.microsoft.com/api/mcp"))
        {
            ToolCallApprovalPolicy = new McpToolCallApprovalPolicy(GlobalMcpToolCallApprovalPolicy.AlwaysRequireApproval)
        };
        
        agentDefinition.Tools.Add(mcpTool);
        
        ProjectsAgentVersion agent = await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            "new-mcp-agent",
            options: new(agentDefinition));

        return agent;
    }
}