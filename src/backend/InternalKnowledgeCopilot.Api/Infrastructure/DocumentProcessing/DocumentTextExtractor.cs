using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;

public interface IDocumentTextExtractor
{
    Task<string> ExtractAsync(string path, string extension, CancellationToken cancellationToken = default);
}

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    public async Task<string> ExtractAsync(string path, string extension, CancellationToken cancellationToken = default)
    {
        return extension.ToLowerInvariant() switch
        {
            ".txt" or ".md" or ".markdown" => await File.ReadAllTextAsync(path, cancellationToken),
            ".docx" => ExtractDocx(path),
            ".pdf" => ExtractPdf(path),
            _ => throw new NotSupportedException($"Unsupported document extension: {extension}")
        };
    }

    private static string ExtractDocx(string path)
    {
        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart?.Document.Body;
        if (body is null)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, body.Descendants<Text>().Select(text => text.Text));
    }

    private static string ExtractPdf(string path)
    {
        var builder = new StringBuilder();
        using var document = PdfDocument.Open(path);
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }
}
