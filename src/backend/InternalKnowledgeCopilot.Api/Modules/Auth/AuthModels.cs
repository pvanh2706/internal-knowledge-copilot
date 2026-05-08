using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Modules.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record AuthUserResponse(Guid Id, string DisplayName, UserRole Role);

public sealed record LoginResponse(string AccessToken, bool MustChangePassword, AuthUserResponse User);
