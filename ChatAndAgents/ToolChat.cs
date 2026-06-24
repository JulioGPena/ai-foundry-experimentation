using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

namespace AI_103.ChatAndAgents;

public class ToolChat
{
#pragma warning disable OPENAI001
    public static async Task Run(IConfiguration configuration)
    {
        var projectClient = new AIProjectClient(
            new Uri(configuration["ProjectUrl"]!),
            new AzureCliCredential());

        var responsesClient = projectClient.ProjectOpenAIClient.GetResponsesClient();
        
        var chat = new ResponseItem[]
        {
            ResponseItem.CreateSystemMessageItem("You're a helpful assistant that always call 'function_tool'.")
        };
        
        var typeInfo = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        }.GetTypeInfo(typeof(MyType));
        
        var schema = typeInfo.GetJsonSchemaAsNode();
        if (schema is JsonObject schemaObject)
        {
            schemaObject["type"] = "object";
        }

        var functionTool = ResponseTool.CreateFunctionTool(
            "function_tool",
            BinaryData.FromString(schema.ToJsonString()),
            false,
            "Test function");

        while (true)
        {
            chat = chat.Where(m => m is MessageResponseItem).ToArray();
            Console.Write("User: ");
            string? userMessage = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(userMessage)) break;
            
            chat = chat.Append(ResponseItem.CreateUserMessageItem(userMessage))
                       .ToArray();

            var responseOptions = CreateResponseOptions(chat, functionTool, configuration);
            
            ResponseResult response = await responsesClient.CreateResponseAsync(responseOptions);
            foreach (var responseItem in response.OutputItems)
            {
                if (responseItem is FunctionCallResponseItem { FunctionName: "function_tool" } call)
                {
                    var argument = JsonSerializer.Deserialize<MyType>(call.FunctionArguments.ToString());
                    var output = new { Success = true };

                    chat = chat.Append(
                        ResponseItem.CreateFunctionCallOutputItem(
                            call.CallId, 
                            JsonSerializer.Serialize(output)))
                               .ToArray();
                    
                    responseOptions = CreateResponseOptions(chat, functionTool, configuration, response.Id);
                    response = await responsesClient.CreateResponseAsync(responseOptions);
                }
            }
            
            string outputText = response.GetOutputText();
            Console.WriteLine($"Assistant: {outputText}");
        }
    }

    private static CreateResponseOptions CreateResponseOptions(
        ResponseItem[]? chat, 
        ResponseTool tool, 
        IConfiguration configuration, 
        string? previousResponseId = null)
    {
        var options = new CreateResponseOptions
        {
            Model = configuration["ModelDeployment"],
            PreviousResponseId = previousResponseId,
            Tools = { tool }
        };

        if (chat is not null)
        {
            foreach (var item in chat)
                options.InputItems.Add(item);
        }

        return options;
    }
    record MyType(string Message);
}