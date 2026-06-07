using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace AI_103.Vision;

public sealed class DocumentIntelligence
{
    public static async Task Run(IConfiguration configuration)
    {
        var documentIntelligenceClient = new DocumentIntelligenceClient(
            new Uri(configuration["FoundryUrl"]!),
            new AzureCliCredential());

        var fileData = await GetFileDataFromDirectoryHierarchy(Directory.GetCurrentDirectory(), "sample-invoice.pdf");

        // You can also use a URL to a document file.
        // var result = documentIntelligenceClient.AnalyzeDocumentAsync(
        //     WaitUntil.Completed,
        //     "prebuilt_invoice",
        //     new Uri("https://github.com/MicrosoftLearning/mslearn-ai-information-extraction/blob/5bcefa25954de7827bd8f55012cd4a4b6c3f2001/Labfiles/03-document-intelligence/prebuilt/sample-invoice/sample-invoice.pdf"));

        var result = await documentIntelligenceClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-invoice",
            fileData);

        foreach (var document in result.Value.Documents)
        {
            Console.WriteLine($"Document of type: {document.DocumentType}");
            foreach (var field in document.Fields)
            {
                if (field.Key.Equals("Items", StringComparison.OrdinalIgnoreCase))
                {
                    var items = field.Value.ValueList;
                    foreach (var item in items)
                    {
                        Console.WriteLine($"Items: {item.Content}");
                    }

                    continue;
                }

                Console.WriteLine($"{field.Key}: {field.Value.Content}");
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