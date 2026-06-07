using Azure;
using Azure.AI.ContentUnderstanding;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace AI_103.Vision;

public sealed class ImageAnalysis
{
    private const string DescriptionField = "Description";
    private const string TagsField = "Tags";
    
    public static async Task Run(IConfiguration configuration)
    {
        var contentUnderstandingClient = new ContentUnderstandingClient(
            new Uri(configuration["FoundryUrl"]!),
            new AzureCliCredential());
        
        var fileData = await GetFileDataFromDirectoryHierarchy(Directory.GetCurrentDirectory(), "guystudying.jpg");
        
        var result = await contentUnderstandingClient.AnalyzeBinaryAsync(
                WaitUntil.Completed,
                configuration["ImageAnalyzer"]!,
                fileData!);

        foreach (var content in result.Value.Contents)
        {
            var tags = content.Fields[TagsField].Value as IEnumerable<ContentField>;
            var tagsText = tags?.Select(t => t.Value?.ToString());
            Console.WriteLine($"Description: {content.Fields[DescriptionField].Value}");
            Console.WriteLine($"Tags: {String.Join(", ", tagsText ?? [])}");
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