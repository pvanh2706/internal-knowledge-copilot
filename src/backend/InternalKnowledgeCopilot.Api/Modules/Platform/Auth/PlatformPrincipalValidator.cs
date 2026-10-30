using System.Globalization;
using System.Security.Claims;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Authentication;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

// Chạy sau khi PlatformJwt đã kiểm tra chữ ký/issuer/audience/hạn. Mỗi request platform kiểm tra lại:
// principal_type, không mang danh tính tenant, provisioning đã được xác minh, tài khoản còn tồn tại và Active,
// token_version khớp DB (đổi mật khẩu/thu hồi làm token cũ vô hiệu) và phiên đầy đủ chỉ khi không còn must_change_password.
// Lỗi DB thì fail closed.
public static class PlatformPrincipalValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null
            || !string.Equals(principal.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value, PrincipalTypes.Platform, StringComparison.Ordinal)
            || principal.HasClaim(claim => claim.Type is TenantClaimTypes.TenantId or TenantClaimTypes.TenantCode or "role" or ClaimTypes.Role))
        {
            context.Fail("platform_principal_type_invalid");
            return;
        }

        var sessionType = principal.FindFirst(PrincipalClaimTypes.PlatformSession)?.Value;
        if (!Guid.TryParse(principal.FindFirst("sub")?.Value, out var adminId)
            || !int.TryParse(principal.FindFirst(PrincipalClaimTypes.TokenVersion)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var tokenVersion)
            || sessionType is not (PlatformSessionTypes.Full or PlatformSessionTypes.PasswordChangeOnly))
        {
            context.Fail("platform_claims_invalid");
            return;
        }

        var services = context.HttpContext.RequestServices;
        string? rejection;
        try
        {
            var provisioning = await services.GetRequiredService<IPlatformProvisioningGate>().CheckAsync(context.HttpContext.RequestAborted);
            rejection = provisioning.IsReady ? null : provisioning.Reason;
            if (rejection is null)
            {
                var admin = await services.GetRequiredService<AppDbContext>().PlatformAdmins
                    .AsNoTracking()
                    .Where(item => item.Id == adminId)
                    .Select(item => new { item.Status, item.TokenVersion, item.MustChangePassword })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                rejection = admin switch
                {
                    null => "account_not_found",
                    { Status: not PlatformAdminStatus.Active } => "account_inactive",
                    _ when admin.TokenVersion != tokenVersion => "token_revoked",
                    { MustChangePassword: true } when sessionType == PlatformSessionTypes.Full => "session_outdated",
                    _ => null,
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Fail("platform_principal_validation_failed");
            return;
        }

        if (rejection is null)
        {
            return;
        }

        await TryAuditRejectionAsync(context.HttpContext, adminId, rejection);
        context.Fail(rejection);
    }

    // Audit từ chối là best-effort: request vẫn bị từ chối kể cả khi không ghi được audit.
    private static async Task TryAuditRejectionAsync(HttpContext httpContext, Guid adminId, string reason)
    {
        try
        {
            var dbContext = httpContext.RequestServices.GetRequiredService<AppDbContext>();
            var actorExists = await dbContext.PlatformAdmins.AsNoTracking().AnyAsync(item => item.Id == adminId, httpContext.RequestAborted);
            httpContext.RequestServices.GetRequiredService<IPlatformAuditWriter>().Stage(new PlatformAuditEntry(
                PlatformAuditActions.SessionRejected,
                PlatformAuditOutcome.Denied,
                actorExists ? adminId : null,
                httpContext.TraceIdentifier,
                new Dictionary<string, string> { [PlatformAuditMetadataKeys.Reason] = reason }));
            await dbContext.SaveChangesAsync(httpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(PlatformPrincipalValidator))
                .LogError(ex, "Không ghi được platform audit cho phiên bị từ chối ({Reason}).", reason);
        }
    }
}
