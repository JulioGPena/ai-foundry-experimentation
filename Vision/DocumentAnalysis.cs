using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.AI.ContentUnderstanding;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace AI_103.Vision;

public sealed class DocumentAnalysis
{
    private const string DescriptionField = "Description";
    private const string TagsField = "Tags";
    
    public static async Task Run(IConfiguration configuration)
    {
        var contentUnderstandingClient = new ContentUnderstandingClient(
            new Uri(configuration["FoundryUrl"]!),
            new AzureCliCredential());
        
        var analyzerDefinition = await GetFileDataFromDirectoryHierarchy(Directory.GetCurrentDirectory(), "businesscard_analyser.json");
        if (analyzerDefinition is null)
        {
            Console.WriteLine("Business card analyzer definition not found.");
            return;
        }

        // Create analyzer only in the first time, then you can reuse it for subsequent runs. You can also create it in the Azure portal and reuse it here.
        // var analyzer = await contentUnderstandingClient.CreateAnalyzerAsync(
        //     WaitUntil.Completed,
        //     configuration["BusinessCardAnalyzer"],
        //     RequestContent.Create(analyzerDefinition.ToArray()));

        while (true)
        {
            Console.Write("Enter the name of the file to analyze: ");
            string? fileName = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(fileName))
            {
                break;
            }
            
            var fileData = await GetFileDataFromDirectoryHierarchy(".", fileName);
            if (fileData is null)
            {
                Console.WriteLine($"File '{fileName}' not found in the current directory or its subdirectories.");
                continue;
            }
            
            var result = await contentUnderstandingClient.AnalyzeBinaryAsync(
                WaitUntil.Completed,
                configuration["BusinessCardAnalyzer"]!,
                fileData);

            foreach (var field in result.Value.Contents.SelectMany(c => c.Fields))
            {
                Console.WriteLine($"{field.Key}: {field.Value.Value}");
            }
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