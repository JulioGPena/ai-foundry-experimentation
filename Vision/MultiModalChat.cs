using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

namespace AI_103.Vision;

#pragma warning disable OPENAI001

public sealed class MultiModalChat
{
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

            BinaryData? fileData = null;
            if (userMessage.Contains("image: ", StringComparison.OrdinalIgnoreCase))
            {
                string image = userMessage[(userMessage.IndexOf("image: ", StringComparison.OrdinalIgnoreCase) + 7)..].Trim();
                
                if (!String.IsNullOrWhiteSpace(image) &&
                    !image.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                    !image.StartsWith("www", StringComparison.OrdinalIgnoreCase))
                {
                    fileData = await GetFileDataFromDirectoryHierarchy(Directory.GetCurrentDirectory(), image);
                    if (fileData is null)
                    {
                        Console.Write($"Assistant: I couldn't find the image '{image}' in the current directory or any subdirectories. Please make sure the file exists and try again.");
                        continue;
                    }
                }
            }

            ResponseItem responseItem;
            if (fileData is not null)
            {
                responseItem = ResponseItem.CreateUserMessageItem([
                    ResponseContentPart.CreateInputTextPart(userMessage),
                    ResponseContentPart.CreateInputImagePart(fileData)
                ]);
            }
            else
            {
                responseItem = ResponseItem.CreateUserMessageItem([
                    ResponseContentPart.CreateInputTextPart(userMessage)
                ]);
            }
            
            chat.Add(responseItem);
            var response = responsesClient.CreateResponseStreamingAsync(
                configuration["MultiModalModelDeployment"],
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
    
    private static async Task<BinaryData?> GetFileDataFromDirectoryHierarchy(string startingDirectory, string fileName)
    {
        foreach (string file in Directory.GetFiles(startingDirectory))
        {
            if (String.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = await File.ReadAllBytesAsync(file);
                string extension = Path.GetExtension(file)
                                       .TrimStart('.')
                                       .ToLowerInvariant();
                
                return new BinaryData(
                    bytes, 
                    mediaType: new System.Net.Mime.ContentType($"image/{extension}").MediaType);
            }
        }
                
        foreach (string directory in Directory.GetDirectories(startingDirectory))
        {
            var fileData = await GetFileDataFromDirectoryHierarchy(directory, fileName);
            if (fileData is not null)
            {
                return fileData;
            }
        }

        return null;
    }
}