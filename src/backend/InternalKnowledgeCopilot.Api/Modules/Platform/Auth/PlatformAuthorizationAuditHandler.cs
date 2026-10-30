using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Authentication;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

// Ghi audit khi một principal platform đã xác thực bị từ chối quyền (ví dụ phiên đổi mật khẩu lần đầu gọi API quản trị).
// Hành vi trả lời (403) giữ nguyên của handler mặc định.
public sealed class PlatformAuthorizationAuditHandler(ILogger<PlatformAuthorizationAuditHandler> logger) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && string.Equals(context.User.FindFirst(PrincipalClaimTypes.PrincipalType)?.Value, PrincipalTypes.Platform, StringComparison.Ordinal)
            && Guid.TryParse(context.User.FindFirst("sub")?.Value, out var adminId))
        {
            var reason = context.User.FindFirst(PrincipalClaimTypes.PlatformSession)?.Value == PlatformSessionTypes.PasswordChangeOnly
                ? "restricted_session"
                : "forbidden";
            try
            {
                context.RequestServices.GetRequiredService<IPlatformAuditWriter>().Stage(new PlatformAuditEntry(
                    PlatformAuditActions.AccessDenied,
                    PlatformAuditOutcome.Denied,
                    adminId,
                    context.TraceIdentifier,
                    new Dictionary<string, string> { [PlatformAuditMetadataKeys.Reason] = reason }));
                await context.RequestServices.GetRequiredService<AppDbContext>().SaveChangesAsync(context.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Không ghi được platform audit cho request bị từ chối quyền ({Reason}).", reason);
            }
        }

        await defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
