using System.Globalization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

public enum PlatformLoginStatus
{
    Succeeded,
    InvalidCredentials,
    Unavailable,
}

public sealed record PlatformLoginResult(PlatformLoginStatus Status, PlatformAccessToken? Token = null, bool MustChangePassword = false);

public enum PlatformPasswordChangeStatus
{
    Changed,
    InvalidCurrentPassword,
    PolicyViolation,
    SessionInvalid,
}

public interface IPlatformAuthService
{
    Task<PlatformLoginResult> LoginAsync(string? email, string? password, string? requestId, CancellationToken cancellationToken = default);

    Task<PlatformMeResponse?> GetProfileAsync(Guid adminId, string sessionType, CancellationToken cancellationToken = default);

    Task<PlatformPasswordChangeStatus> ChangePasswordAsync(
        Guid adminId,
        int tokenVersion,
        string? currentPassword,
        string? newPassword,
        string? requestId,
        CancellationToken cancellationToken = default);
}

// Đăng nhập local chỉ dựa trên platform_admins (không bao giờ dùng users/UserRole.Admin của tenant).
// Mọi thay đổi trạng thái tài khoản ghi cùng audit trong một transaction SQLite: Microsoft.Data.Sqlite mở
// transaction bằng BEGIN IMMEDIATE nên các lần đăng nhập sai đồng thời được tuần tự hóa, không mất lượt đếm.
// Nếu audit bắt buộc không ghi được thì cả transaction rollback và request thất bại (không báo thành công giả).
public sealed class PlatformAuthService(
    AppDbContext dbContext,
    IPlatformPasswordHasher passwordHasher,
    IPlatformJwtTokenService tokenService,
    IPlatformProvisioningGate provisioningGate,
    IPlatformAuditWriter auditWriter,
    IOptions<PlatformAuthOptions> options,
    ILogger<PlatformAuthService> logger) : IPlatformAuthService
{
    public const int MinPasswordLength = 12;
    public const int MaxPasswordLength = 128;

    public async Task<PlatformLoginResult> LoginAsync(string? email, string? password, string? requestId, CancellationToken cancellationToken = default)
    {
        password ??= string.Empty;
        var provisioning = await provisioningGate.CheckAsync(cancellationToken);
        if (!provisioning.IsReady)
        {
            if (provisioning.BootstrapCompleted)
            {
                await SaveStandaloneAuditAsync(PlatformAuditActions.LoginDenied, PlatformAuditOutcome.Denied, null, requestId, provisioning.Reason, cancellationToken);
            }
            else
            {
                // Trước bootstrap: chỉ log (không email/mật khẩu), không ghi platform_audit_logs để giữ điều kiện bootstrap lần đầu.
                logger.LogWarning(
                    "Platform login refused before bootstrap completion: {Reason}. RequestId: {RequestId}",
                    provisioning.Reason,
                    requestId);
            }

            return new PlatformLoginResult(PlatformLoginStatus.Unavailable);
        }

        var normalizedEmail = PlatformEmailNormalizer.TryNormalize(email);
        var candidate = normalizedEmail is null || password.Length > MaxPasswordLength
            ? null
            : await dbContext.PlatformAdmins.AsNoTracking().SingleOrDefaultAsync(item => item.EmailNormalized == normalizedEmail, cancellationToken);
        if (candidate is null)
        {
            passwordHasher.SimulateVerification(password);
            await SaveStandaloneAuditAsync(PlatformAuditActions.LoginFailed, PlatformAuditOutcome.Failed, null, requestId, "invalid_credentials", cancellationToken);
            return new PlatformLoginResult(PlatformLoginStatus.InvalidCredentials);
        }

        // Luôn verify (kể cả tài khoản bị khóa/vô hiệu) để thời gian phản hồi không lộ trạng thái tài khoản.
        var passwordValid = passwordHasher.VerifyPassword(candidate.PasswordHash, password);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var admin = await dbContext.PlatformAdmins.SingleAsync(item => item.Id == candidate.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        ReleaseExpiredLock(admin, now);

        if (admin.Status != PlatformAdminStatus.Active)
        {
            var reason = admin.Status == PlatformAdminStatus.Disabled ? "account_disabled" : "account_locked";
            Stage(PlatformAuditActions.LoginDenied, PlatformAuditOutcome.Denied, admin.Id, requestId, Metadata(reason));
            await CommitAsync(transaction, cancellationToken);
            return new PlatformLoginResult(PlatformLoginStatus.InvalidCredentials);
        }

        if (!passwordValid || !string.Equals(admin.PasswordHash, candidate.PasswordHash, StringComparison.Ordinal))
        {
            RegisterFailedAttempt(admin, now, PlatformAuditActions.LoginFailed, "invalid_credentials", requestId);
            await CommitAsync(transaction, cancellationToken);
            return new PlatformLoginResult(PlatformLoginStatus.InvalidCredentials);
        }

        admin.FailedLoginCount = 0;
        admin.LockedUntil = null;
        admin.LastLoginAt = now;
        admin.UpdatedAt = now;
        var token = tokenService.CreateAccessToken(admin);
        Stage(
            PlatformAuditActions.LoginSucceeded,
            PlatformAuditOutcome.Success,
            admin.Id,
            requestId,
            new Dictionary<string, string> { [PlatformAuditMetadataKeys.SessionType] = token.SessionType });
        await CommitAsync(transaction, cancellationToken);
        return new PlatformLoginResult(PlatformLoginStatus.Succeeded, token, admin.MustChangePassword);
    }

    public async Task<PlatformMeResponse?> GetProfileAsync(Guid adminId, string sessionType, CancellationToken cancellationToken = default)
    {
        var admin = await dbContext.PlatformAdmins.AsNoTracking().SingleOrDefaultAsync(item => item.Id == adminId, cancellationToken);
        return admin is null
            ? null
            : new PlatformMeResponse(admin.Id, admin.EmailNormalized, admin.DisplayName, admin.Status, admin.MustChangePassword, sessionType);
    }

    public async Task<PlatformPasswordChangeStatus> ChangePasswordAsync(
        Guid adminId,
        int tokenVersion,
        string? currentPassword,
        string? newPassword,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        currentPassword ??= string.Empty;
        if (newPassword is null || newPassword.Length is < MinPasswordLength or > MaxPasswordLength || string.IsNullOrWhiteSpace(newPassword))
        {
            await SaveStandaloneAuditAsync(PlatformAuditActions.PasswordChangeFailed, PlatformAuditOutcome.Failed, adminId, requestId, "password_policy", cancellationToken);
            return PlatformPasswordChangeStatus.PolicyViolation;
        }

        var candidate = await dbContext.PlatformAdmins.AsNoTracking().SingleOrDefaultAsync(item => item.Id == adminId, cancellationToken);
        if (candidate is null)
        {
            return PlatformPasswordChangeStatus.SessionInvalid;
        }

        var currentValid = currentPassword.Length <= MaxPasswordLength && passwordHasher.VerifyPassword(candidate.PasswordHash, currentPassword);
        var reused = currentValid && passwordHasher.VerifyPassword(candidate.PasswordHash, newPassword);
        var newHash = currentValid && !reused ? passwordHasher.HashPassword(newPassword) : null;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var admin = await dbContext.PlatformAdmins.SingleAsync(item => item.Id == adminId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (admin.Status != PlatformAdminStatus.Active
            || admin.TokenVersion != tokenVersion
            || !string.Equals(admin.PasswordHash, candidate.PasswordHash, StringComparison.Ordinal))
        {
            Stage(PlatformAuditActions.SessionRejected, PlatformAuditOutcome.Denied, admin.Id, requestId, Metadata("session_outdated"));
            await CommitAsync(transaction, cancellationToken);
            return PlatformPasswordChangeStatus.SessionInvalid;
        }

        if (!currentValid)
        {
            RegisterFailedAttempt(admin, now, PlatformAuditActions.PasswordChangeFailed, "invalid_current_password", requestId);
            await CommitAsync(transaction, cancellationToken);
            return PlatformPasswordChangeStatus.InvalidCurrentPassword;
        }

        if (newHash is null)
        {
            Stage(PlatformAuditActions.PasswordChangeFailed, PlatformAuditOutcome.Failed, admin.Id, requestId, Metadata("password_reused"));
            await CommitAsync(transaction, cancellationToken);
            return PlatformPasswordChangeStatus.PolicyViolation;
        }

        admin.PasswordHash = newHash;
        admin.MustChangePassword = false;
        admin.FailedLoginCount = 0;
        admin.TokenVersion += 1;
        admin.UpdatedAt = now;
        Stage(PlatformAuditActions.PasswordChanged, PlatformAuditOutcome.Success, admin.Id, requestId);
        Stage(
            PlatformAuditActions.TokensRevoked,
            PlatformAuditOutcome.Success,
            admin.Id,
            requestId,
            new Dictionary<string, string>
            {
                [PlatformAuditMetadataKeys.Reason] = "password_changed",
                [PlatformAuditMetadataKeys.TokenVersion] = admin.TokenVersion.ToString(CultureInfo.InvariantCulture),
            });
        await CommitAsync(transaction, cancellationToken);
        return PlatformPasswordChangeStatus.Changed;
    }

    // Khóa có thời hạn đã hết: mở lại và bắt đầu cửa sổ đếm mới.
    private static void ReleaseExpiredLock(PlatformAdminEntity admin, DateTimeOffset now)
    {
        if (admin.Status == PlatformAdminStatus.Locked && admin.LockedUntil is { } lockedUntil && lockedUntil <= now)
        {
            admin.Status = PlatformAdminStatus.Active;
            admin.LockedUntil = null;
            admin.FailedLoginCount = 0;
            admin.UpdatedAt = now;
        }
    }

    private void RegisterFailedAttempt(PlatformAdminEntity admin, DateTimeOffset now, string action, string reason, string? requestId)
    {
        var settings = options.Value;
        admin.FailedLoginCount += 1;
        admin.UpdatedAt = now;
        Stage(
            action,
            PlatformAuditOutcome.Failed,
            admin.Id,
            requestId,
            new Dictionary<string, string>
            {
                [PlatformAuditMetadataKeys.Reason] = reason,
                [PlatformAuditMetadataKeys.FailedLoginCount] = admin.FailedLoginCount.ToString(CultureInfo.InvariantCulture),
            });

        if (admin.Status == PlatformAdminStatus.Active && admin.FailedLoginCount >= settings.MaxFailedLoginAttempts)
        {
            admin.Status = PlatformAdminStatus.Locked;
            admin.LockedUntil = now.AddMinutes(settings.LockoutMinutes);
            Stage(
                PlatformAuditActions.Lockout,
                PlatformAuditOutcome.Denied,
                admin.Id,
                requestId,
                new Dictionary<string, string>
                {
                    [PlatformAuditMetadataKeys.FailedLoginCount] = admin.FailedLoginCount.ToString(CultureInfo.InvariantCulture),
                    [PlatformAuditMetadataKeys.LockoutMinutes] = settings.LockoutMinutes.ToString(CultureInfo.InvariantCulture),
                });
        }
    }

    private async Task SaveStandaloneAuditAsync(
        string action,
        PlatformAuditOutcome outcome,
        Guid? actorId,
        string? requestId,
        string reason,
        CancellationToken cancellationToken)
    {
        Stage(action, outcome, actorId, requestId, Metadata(reason));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CommitAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private void Stage(string action, PlatformAuditOutcome outcome, Guid? actorId, string? requestId, IReadOnlyDictionary<string, string>? metadata = null)
    {
        auditWriter.Stage(new PlatformAuditEntry(action, outcome, actorId, requestId, metadata));
    }

    private static Dictionary<string, string> Metadata(string reason)
    {
        return new Dictionary<string, string> { [PlatformAuditMetadataKeys.Reason] = reason };
    }
}
