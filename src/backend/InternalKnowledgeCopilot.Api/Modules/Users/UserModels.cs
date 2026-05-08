using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Users;

public sealed record UserListItemResponse(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    Guid? PrimaryTeamId,
    string? PrimaryTeamName,
    bool MustChangePassword,
    bool IsActive);

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    UserRole Role,
    Guid? PrimaryTeamId,
    string InitialPassword);

public sealed record UpdateUserRequest(
    string? DisplayName,
    UserRole? Role,
    Guid? PrimaryTeamId,
    bool? IsActive);
