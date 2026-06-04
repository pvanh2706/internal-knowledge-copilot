using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Connectors;

public sealed record ExternalConnectorContext(
    Guid TenantId,
    Guid ApplicationId,
    Uri BaseUrl,
    IntegrationAuthMode AuthMode,
    string? SecretReference,
    string? ApiKey);

public sealed record ExternalDocumentContentResponse(
    string ExternalId,
    string Content,
    string? ContentType,
    string? ContentHash,
    string? MetadataJson);

public sealed record ExternalObjectContextResponse(
    string ObjectType,
    string ExternalObjectId,
    string ContextJson,
    string? MetadataJson);

public sealed record ExternalAccessCheckRequest(
    string ObjectType,
    string ExternalObjectId,
    string SubjectType,
    string SubjectId,
    ExternalAclPermission Permission);

public sealed record ExternalAccessCheckResponse(
    bool IsAllowed,
    string? Reason,
    DateTimeOffset CheckedAt);

public sealed record ExternalActionValidationRequest(
    string ActionType,
    string ObjectType,
    string ExternalObjectId,
    string PayloadJson,
    string IdempotencyKey);

public sealed record ExternalActionValidationResponse(
    bool IsValid,
    string? Reason,
    string? NormalizedPayloadJson);

public sealed record ExternalActionExecutionRequest(
    string ActionType,
    string ObjectType,
    string ExternalObjectId,
    string PayloadJson,
    string IdempotencyKey);

public sealed record ExternalActionExecutionResponse(
    bool Succeeded,
    string? ExternalExecutionId,
    string? ResultJson,
    string? Error);
