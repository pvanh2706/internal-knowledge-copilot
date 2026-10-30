namespace InternalKnowledgeCopilot.Api.Infrastructure.Authentication;

// S01-B (Baseline v1.0 §3): hai scheme độc lập, không scheme nào được chấp nhận thay cho scheme kia.
public static class AppAuthenticationSchemes
{
    public const string TenantJwt = "TenantJwt";

    public const string PlatformJwt = "PlatformJwt";
}

public static class PrincipalClaimTypes
{
    public const string PrincipalType = "principal_type";

    public const string TokenVersion = "token_version";

    public const string PlatformSession = "platform_session";
}

public static class PrincipalTypes
{
    public const string Tenant = "tenant";

    public const string Platform = "platform";
}

public static class PlatformSessionTypes
{
    public const string Full = "full";

    // Phiên đăng nhập lần đầu (must_change_password): chỉ được xem danh tính và đổi mật khẩu.
    public const string PasswordChangeOnly = "password_change";
}

public static class AppAuthorizationPolicies
{
    public const string TenantAuthenticated = "TenantAuthenticated";

    public const string TenantAdminOnly = "TenantAdminOnly";

    public const string PlatformAdminOnly = "PlatformAdminOnly";

    public const string PlatformPasswordChange = "PlatformPasswordChange";
}
