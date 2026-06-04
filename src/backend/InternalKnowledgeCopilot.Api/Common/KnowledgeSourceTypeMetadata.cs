namespace InternalKnowledgeCopilot.Api.Common;

public static class KnowledgeSourceTypeMetadata
{
    public static string ToMetadataValue(KnowledgeSourceType sourceType)
    {
        return sourceType switch
        {
            KnowledgeSourceType.ExternalObject => "external_object",
            _ => sourceType.ToString().ToLowerInvariant(),
        };
    }

    public static bool TryParse(string? value, out KnowledgeSourceType sourceType)
    {
        sourceType = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace("_", string.Empty).Replace("-", string.Empty);
        return Enum.TryParse(normalized, ignoreCase: true, out sourceType);
    }
}
