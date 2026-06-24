using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

namespace AI_103.ChatAndAgents;

public class SimpleChat
{
    #pragma warning disable OPENAI001
    public static async Task Run(IConfiguration configuration)
    {
        var projectClient = new AIProjectClient(
            new Uri(configuration["ProjectUrl"]!),
            new AzureCliCredential());
        
        var responsesClient = projectClient.ProjectOpenAIClient.GetProjectResponsesClient();
        
        var chat = new List<ResponseItem>
        {
            ResponseItem.CreateSystemMessageItem("You're a helpful assistant.")
        };

        while (true)
        {
            Console.Write("User: ");
            string? userMessage = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(userMessage)) break;
            
            chat.Add(ResponseItem.CreateUserMessageItem(userMessage));
            var response = responsesClient.CreateResponseStreamingAsync(
                configuration["ModelDeployment"],
                chat);

            int updateCount = 0;
            await foreach (var chunk in response)
            {
                if (updateCount == 0) Console.Write("Assistant: ");
                if (chunk is StreamingResponseOutputTextDeltaUpdate textDelta)
                {
                    Console.Write(textDelta.Delta);
                }

                updateCount++;
            }
            
            Console.WriteLine();
        }
    }
}