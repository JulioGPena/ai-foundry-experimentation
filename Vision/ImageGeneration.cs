using System;
using System.ClientModel;
using System.IO;
using System.Threading.Tasks;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Images;

namespace AI_103.Vision;

#pragma warning disable OPENAI001

public sealed class ImageGeneration
{
    public static async Task Run(IConfiguration configuration)
    {
        var imageClient = new ImageClient(
            credential: new ApiKeyCredential(configuration["ProjectApiKey"]!),
            model: configuration["ImageGenerationModelDeployment"],
            options: new OpenAIClientOptions()
            {
                Endpoint = new Uri(configuration["OpenAiUrl"]!),
            }
        );
        
        while (true)
        {
            Console.Write("User: ");
            string? userMessage = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(userMessage)) break;
    
            
            GeneratedImage createdImage = await imageClient.GenerateImageAsync(
                userMessage,
                new ImageGenerationOptions
                {
                    Size = GeneratedImageSize.W1024xH1024,
                    OutputFileFormat = GeneratedImageFileFormat.Jpeg
                });
            
            string fileName = $"generated_image_{Guid.NewGuid()}.jpg";
            File.WriteAllBytes(fileName, createdImage.ImageBytes);
    
            Console.Write($"Assistant: created image file '{fileName}'.");
            Console.WriteLine();
        }
    }
    
    // // This returns 404 even though the model is deployed. Seems like
    // // the API is not yet supported coming from AIProjectClient?.
    // public static async Task Run(IConfiguration configuration)
    // {
    //     var projectClient = new AIProjectClient(
    //         new Uri(configuration["ProjectUrl"]!),
    //         new AzureCliCredential());
    //
    //     var imageClient = projectClient.ProjectOpenAIClient
    //                                    .GetImageClient(configuration["ImageGenerationModelDeployment"]);
    //     
    //     while (true)
    //     {
    //         Console.Write("User: ");
    //         string? userMessage = Console.ReadLine();
    //         if (String.IsNullOrWhiteSpace(userMessage)) break;
    //
    //         
    //         GeneratedImage createdImage = await imageClient.GenerateImageAsync(
    //             userMessage,
    //             new ImageGenerationOptions
    //             {
    //                 Size = GeneratedImageSize.W1024xH1024,
    //                 OutputFileFormat = GeneratedImageFileFormat.Jpeg
    //             });
    //         
    //         string fileName = $"generated_image_{Guid.NewGuid()}.jpg";
    //         File.WriteAllBytes(fileName, createdImage.ImageBytes);
    //
    //         Console.Write($"Assistant: created image file '{fileName}'.");
    //         Console.WriteLine();
    //     }
    // }
}