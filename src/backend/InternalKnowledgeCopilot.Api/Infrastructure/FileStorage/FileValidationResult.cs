namespace InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;

public sealed record FileValidationResult(bool IsValid, string? ErrorCode, string? Message)
{
    public static FileValidationResult Valid() => new(true, null, null);

    public static FileValidationResult Invalid(string errorCode, string message) => new(false, errorCode, message);
}
