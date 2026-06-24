using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.AI.TextAnalytics;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace AI_103.NaturalLanguage;

public sealed class TextAnalysis
{
    public static async Task Run(IConfiguration configuration)
    {
        var textAnalyticsClient = new TextAnalyticsClient(
            new Uri(configuration["FoundryUrl"]!),
            new AzureCliCredential());
        
        TextDocumentInput[] documents = GetDocuments();

        // These two can be done in one call, example below
        // RecognizeEntitiesResultCollection entitiess = await textAnalyticsClient.RecognizeEntitiesBatchAsync(documents);
        // RecognizePiiEntitiesResultCollection piis = await textAnalyticsClient.RecognizePiiEntitiesBatchAsync(documents);

        Dictionary<string, DetectLanguageResult> languages = 
            (await textAnalyticsClient.DetectLanguageBatchAsync(documents.Select(d => new DetectLanguageInput(d.Id, d.Text))))
            .Value
            .ToDictionary(l => l.Id);

        var actions = new TextAnalyticsActions
        {
            RecognizeEntitiesActions = new List<RecognizeEntitiesAction> { new() },
            RecognizePiiEntitiesActions = new List<RecognizePiiEntitiesAction> { new () }
        };

        var operation = await textAnalyticsClient.StartAnalyzeActionsAsync(documents, actions);
        await operation.WaitForCompletionAsync();

        Dictionary<string, RecognizeEntitiesResult> entities = new();
        Dictionary<string, RecognizePiiEntitiesResult> piis = new();
        
        await foreach (var analysis in operation.Value)
        {
            var entitiesPage = analysis.RecognizeEntitiesResults
                                       .SelectMany(r => r.DocumentsResults);

            foreach (var e in entitiesPage)
                entities[e.Id] = e;

            var piisPage = analysis.RecognizePiiEntitiesResults
                                   .SelectMany(r => r.DocumentsResults);

            foreach (var p in piisPage)
                piis[p.Id] = p;
        }

        foreach (var document in documents)
        {
            DetectLanguageResult? languageResult = languages.GetValueOrDefault(document.Id);
            RecognizeEntitiesResult? entityResult = entities.GetValueOrDefault(document.Id);
            RecognizePiiEntitiesResult? piiResult = piis.GetValueOrDefault(document.Id);

            string language = languageResult?.PrimaryLanguage.Name ?? "Unable to detect language";
            string extractedEntities = entityResult?.Entities.Any() ?? false ? String.Join(" - ", entityResult.Entities.Select(e => $"{e.Text}, Category: {e.Category}, Confidence: {e.ConfidenceScore}")) : "No entities recognized";
            string extractedPiis = piiResult?.Entities.Any() ?? false ? String.Join(" - ", piiResult.Entities.Select(e => $"{e.Text}, Category: {e.Category}, Confidence: {e.ConfidenceScore}")) : "No PIIs detected";
            string redactedText = piiResult?.Entities.Any() ?? false ? piiResult.Entities.RedactedText : "";
            
            Console.WriteLine($"Document ID: {document.Id}");
            Console.WriteLine($"Language: {language}");
            Console.WriteLine($"Entities: {extractedEntities}");
            Console.WriteLine($"PII Entities: {extractedPiis}");
            Console.WriteLine($"Redacted text: {redactedText}");
            Console.WriteLine();
        }
    }

    private static TextDocumentInput[] GetDocuments() =>
    [
        // --- English (PII + Entities)
        new("1",
            "John Doe lives at 123 Main St, New York, NY. His email is john.doe@example.com and phone is +1-212-555-1234."),
        new("2", "Microsoft Corporation was founded by Bill Gates and Paul Allen in 1975."),
        new("3", "My credit card number is 4111 1111 1111 1111 and expires on 12/28."),
        new("4", "SSN: 123-45-6789, Passport: X12345678."),
        new("5", "Contact support at support@contoso.com or call (800) 555-0199."),

        // --- Portuguese (your locale)
        new("6", "O cliente João Silva mora na Avenida da Liberdade, Lisboa. Email: joao.silva@email.pt."),
        new("7", "O NIF é 123456789 e o telefone é +351 912 345 678."),
        new("8", "A empresa EDP tem sede em Portugal e atua no setor de energia."),

        // --- Spanish
        new("9", "María García vive en Madrid. Su correo es maria.garcia@email.es y su teléfono es +34 600 123 456."),
        new("10", "El DNI es 12345678Z y trabaja para Telefónica."),

        // --- French
        new("11", "Jean Dupont habite à Paris. Son email est jean.dupont@email.fr."),
        new("12", "Le numéro de sécurité sociale est 1 84 12 75 123 456."),

        // --- German
        new("13", "Hans Müller wohnt in Berlin. Seine E-Mail ist hans.mueller@email.de."),

        // --- Mixed technical / logs (useful for real scenarios)
        new("14", "[ERROR] Payment failed for user jane.doe@company.com using card 5555-4444-3333-1111 on 2026-05-12."),
        new("15", "[INFO] User login from IP 192.168.1.1 by admin@corp.local at 10:45 AM."),

        // --- Dates, currency, products
        new("16", "Apple released the iPhone 15 in September 2023 for around $999."),
        new("17", "The meeting with Google will happen on 2026-06-10 in London."),

        // --- Edge cases
        new("18", "Call me maybe 😊"),
        new("19", "1234567890"),
        new("20", "email@domain"),

        // --- Asian languages (for language detection)
        new("21", "这是一个测试文本，其中包含一个电子邮件 test@example.com 和一个电话号码 13800138000。"),
        new("22", "これはテスト文章です。メールは test@test.co.jp です。"),

        // --- Arabic
        new("23", "هذا نص تجريبي يحتوي على بريد إلكتروني example@test.com ورقم هاتف +971501234567.")
    ];
}