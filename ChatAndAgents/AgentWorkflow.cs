using System;
using System.Threading.Tasks;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

namespace AI_103.ChatAndAgents;

#pragma warning disable OPENAI001

public sealed class AgentWorkflow
{
    public static async Task Run(IConfiguration configuration)
    {
        var projectClient = new AIProjectClient(
            new Uri(configuration["ProjectUrl"]!),
            new AzureCliCredential());
        
        var agentReference = new AgentReference(configuration["AgentWorkflowName"]!);
        
        ProjectConversation conversation = await projectClient.ProjectOpenAIClient.GetProjectConversationsClient()
                                                              .CreateProjectConversationAsync();
        
        var responsesClient = projectClient.ProjectOpenAIClient
                                           .GetProjectResponsesClientForAgent(agentReference, conversation.Id);
        
        while (true)
        {
            Console.Write("User: ");
            
            string? userMessage = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(userMessage)) break;
            
            ResponseResult? response = await responsesClient.CreateResponseAsync([ResponseItem.CreateUserMessageItem(userMessage)]);
            foreach (AgentResponseItem responseItem in response.OutputItems)
            {
                // This will probably change in the future so we can have a clear way of submiting responses to 'Question' actions in agent workflows.
                if (responseItem is AgentWorkflowPreviewActionResponseItem { Kind: "Question" })
                {
                    Console.WriteLine($"Assistant: {response?.GetOutputText()}");
                    Console.Write("User: ");
            
                    userMessage = Console.ReadLine();
                    if (String.IsNullOrWhiteSpace(userMessage)) break;
                    
                    response = await responsesClient.CreateResponseAsync([ResponseItem.CreateUserMessageItem(userMessage)]);
                }
            }
            
            Console.WriteLine($"Assistant: {response?.GetOutputText()}");
            Console.WriteLine();
        }
    }
}