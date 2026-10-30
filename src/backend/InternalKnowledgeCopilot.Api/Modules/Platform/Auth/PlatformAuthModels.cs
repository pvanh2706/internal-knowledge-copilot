using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

public sealed record PlatformLoginRequest(string Email, string Password);

public sealed record PlatformLoginResponse(string AccessToken, DateTimeOffset ExpiresAt, bool MustChangePassword, string SessionType);

public sealed record PlatformChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record PlatformMeResponse(
    Guid Id,
    string Email,
    string DisplayName,
    PlatformAdminStatus Status,
    bool MustChangePassword,
    string SessionType);
