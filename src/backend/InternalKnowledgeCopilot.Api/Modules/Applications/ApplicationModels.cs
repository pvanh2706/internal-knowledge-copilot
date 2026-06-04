using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Applications;

public sealed record ApplicationResponse(
    Guid Id,
    Guid TenantId,
    string TenantCode,
    string Code,
    string Name,
    ApplicationType ApplicationType,
    string? BaseUrl,
    ApplicationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateApplicationRequest(
    Guid TenantId,
    string Code,
    string Name,
    ApplicationType ApplicationType,
    string? BaseUrl,
    ApplicationStatus? Status);

public sealed record UpdateApplicationRequest(
    string? Name,
    ApplicationType? ApplicationType,
    string? BaseUrl,
    ApplicationStatus? Status);
