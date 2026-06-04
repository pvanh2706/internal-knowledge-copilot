using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Tenants;

public sealed record TenantResponse(
    Guid Id,
    string Code,
    string Name,
    TenantStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateTenantRequest(
    string Name,
    string Code,
    TenantStatus? Status);

public sealed record UpdateTenantRequest(
    string? Name,
    TenantStatus? Status);
