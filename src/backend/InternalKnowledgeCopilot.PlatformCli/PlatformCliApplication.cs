using System.Globalization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.PlatformCli.Bootstrap;
using InternalKnowledgeCopilot.PlatformCli.Preflight;
using InternalKnowledgeCopilot.PlatformCli.Recovery;

namespace InternalKnowledgeCopilot.PlatformCli;

// CLI operator (C1). Output chỉ gồm mã lý do, định danh (GUID) và trạng thái; không bao giờ in mật khẩu, hash, token,
// integrity key hay message lỗi thô của AWS/SQLite.
public sealed class PlatformCliApplication(IOperatorConsole console, PlatformCliHostOptions hostOptions)
{
    public static Task<int> RunAsync(string[] args, IOperatorConsole console, PlatformCliHostOptions? hostOptions = null, CancellationToken cancellationToken = default)
    {
        return new PlatformCliApplication(console, hostOptions ?? new PlatformCliHostOptions()).RunAsync(args, cancellationToken);
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var arguments = CliArguments.Parse(args);
            using var context = PlatformCliContext.Create(arguments.Get("app-dir"), hostOptions);
            var requestId = $"cli-{arguments.Command}-{Guid.NewGuid():N}";
            return arguments.Command switch
            {
                CliArguments.Status => await StatusAsync(context, detailed: false, cancellationToken),
                CliArguments.RecoveryCheck => await StatusAsync(context, detailed: true, cancellationToken),
                CliArguments.Bootstrap => await BootstrapAsync(arguments, context, requestId, cancellationToken),
                CliArguments.RecoverAdmin => await RecoverAdminAsync(arguments, context, requestId, cancellationToken),
                _ => throw new CliUsageException("unknown_command"),
            };
        }
        catch (CliUsageException ex)
        {
            console.WriteError($"error: {ex.Reason}");
            console.WriteError(CliArguments.Usage);
            return CliExitCodes.UsageError;
        }
        catch (CliConfigurationException ex)
        {
            console.WriteError($"configuration_invalid: {ex.Message}");
            return CliExitCodes.ConfigurationInvalid;
        }
        catch (ProvisioningAuthorityUnavailableException ex)
        {
            console.WriteError($"authority_unavailable: {ex.Reason}");
            return CliExitCodes.AuthorityUnavailable;
        }
        catch (OperationCanceledException)
        {
            console.WriteError("cancelled");
            return CliExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            console.WriteError($"internal_error ({ex.GetType().Name})");
            return CliExitCodes.InternalError;
        }
    }

    // status/recovery-check: SQLite mở Mode=ReadOnly, authority chỉ đọc, không tạo transition client.
    private async Task<int> StatusAsync(PlatformCliContext context, bool detailed, CancellationToken cancellationToken)
    {
        var inspection = await context.InspectAsync(cancellationToken);
        var read = context.Authority is null ? null : await context.GetReader().ReadAsync(cancellationToken);
        var diagnosis = ProvisioningReconciliation.Diagnose(inspection, read, context.Authority?.Identity);

        console.WriteLine(detailed ? "Platform provisioning recovery check" : "Platform provisioning status");
        Field("Environment", context.EnvironmentName);
        Field("Database", context.DatabasePath + (inspection is null ? " (not found)" : string.Empty));
        Field("Authority provider", context.Authority?.ProviderCode ?? "not configured");
        if (context.Authority is { } authority)
        {
            Field("Deployment id", authority.Identity.DeploymentId.ToString("D"));
            Field("Authority id", authority.Identity.AuthorityId);
        }

        if (inspection is not null)
        {
            Field("Schema", inspection.SchemaProblem ?? "current");
            Field("Bootstrap state", inspection.State is { } state
                ? $"{state.BootstrapStatus} (admin {state.BootstrapAdminId:D}, completed {Format(state.BootstrapCompletedAt)}, state_version {state.StateVersion})"
                : inspection.SchemaProblem is null ? "absent" : "unknown");
            Field("Platform admins", Count(inspection));
            Field("Platform audit rows", inspection.AuditCount.ToString(CultureInfo.InvariantCulture));
        }

        Field("Authority record", read switch
        {
            null => "n/a",
            { Record: { } record } => $"{record.Status} (version {record.Version}, state_version {record.StateVersion?.ToString(CultureInfo.InvariantCulture) ?? "-"})",
            _ => $"{read.Outcome} ({read.Reason})",
        });

        if (detailed)
        {
            if (read?.Record is { } record)
            {
                Field("Authorization id", record.AuthorizationId);
                Field("Reservation id", record.ReservationId?.ToString("D") ?? "-");
                Field("Reserved admin id", record.ReservedAdminId?.ToString("D") ?? "-");
                Field("Pending recovery", record.RecoveryId is { } recoveryId
                    ? $"{recoveryId:D} ({record.RecoveryPurpose}, target {record.RecoveryTargetAdminId?.ToString("D") ?? "bootstrap admin"})"
                    : "-");
                Field("Last recovery", record.LastRecoveryId is { } lastRecovery
                    ? $"{lastRecovery:D} at {Format(record.LastRecoveredAt)} (total {record.RecoveryCount})"
                    : "-");
            }

            if (inspection is not null)
            {
                Field("Bootstrap audit reservation", inspection.BootstrapAuditReservationId?.ToString("D") ?? "-");
                Field("Applied recoveries (SQLite)", inspection.AppliedRecoveryIds.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var admin in inspection.Admins)
                {
                    console.WriteLine($"    - {admin.Id:D} {admin.Email} status={admin.Status} must_change_password={admin.MustChangePassword} " +
                        $"locked_until={Format(admin.LockedUntil)} token_version={admin.TokenVersion}");
                }
            }
        }

        Field("Condition", $"{diagnosis.Condition} ({diagnosis.Reason})");
        Field("Next action", diagnosis.Action);
        if (detailed)
        {
            console.WriteLine("  Note: restoring an older Completed SQLite snapshot with the same state_version is NOT detectable (C-G09 OPEN).");
        }

        return diagnosis.Condition switch
        {
            ProvisioningCondition.Ready => CliExitCodes.Success,
            ProvisioningCondition.AuthorityUnavailable => CliExitCodes.AuthorityUnavailable,
            _ => CliExitCodes.Denied,
        };
    }

    private async Task<int> BootstrapAsync(CliArguments arguments, PlatformCliContext context, string requestId, CancellationToken cancellationToken)
    {
        if (RequireMutationPreconditions(context) is { } blocked)
        {
            return blocked;
        }

        var service = new PlatformBootstrapService(context, context.CreateTransitionClient(console));
        var preflight = await service.PreflightAsync(cancellationToken);
        if (preflight.Result.Outcome != OperationOutcome.Success)
        {
            await TryAuditDenialAsync(context, preflight.Inspection, PlatformAuditActions.BootstrapDenied, preflight.Result.Reason, requestId);
            return Report(preflight.Result);
        }

        if (!console.IsInteractive)
        {
            throw new CliUsageException("interactive_terminal_required");
        }

        var email = PlatformEmailNormalizer.TryNormalize(arguments.Get("email") ?? console.ReadLine("Platform admin email: "))
            ?? throw new CliUsageException("email_invalid");
        var displayName = (arguments.Get("display-name") ?? console.ReadLine("Display name: "))?.Trim();
        if (string.IsNullOrEmpty(displayName) || displayName.Length > 200)
        {
            throw new CliUsageException("display_name_invalid");
        }

        var password = ReadNewPassword();
        if (password is null)
        {
            return CliExitCodes.UsageError;
        }

        console.WriteLine($"About to create the first Platform Admin '{email}' for deployment {context.Authority!.Identity.DeploymentId:D} " +
            $"(authority {context.Authority.ProviderCode}, database {context.DatabasePath}).");
        if (!Confirm("BOOTSTRAP"))
        {
            console.WriteError("cancelled");
            return CliExitCodes.Cancelled;
        }

        var passwordHash = new PlatformPasswordHasher().HashPassword(password);
        var result = await service.ExecuteAsync(new BootstrapInput(email, displayName, passwordHash), requestId, cancellationToken);
        if (result.Outcome == OperationOutcome.Success)
        {
            console.WriteLine("The first login issues a restricted password-change session; the password must be changed before administration.");
        }

        return Report(result);
    }

    private async Task<int> RecoverAdminAsync(CliArguments arguments, PlatformCliContext context, string requestId, CancellationToken cancellationToken)
    {
        if (RequireMutationPreconditions(context) is { } blocked)
        {
            return blocked;
        }

        var service = new PlatformRecoveryService(context, context.CreateTransitionClient(console));
        var preflight = await service.PreflightAsync(cancellationToken);
        if (preflight.Result.Outcome != OperationOutcome.Success)
        {
            await TryAuditDenialAsync(context, preflight.Inspection, PlatformAuditActions.RecoveryDenied, preflight.Result.Reason, requestId);
            return Report(preflight.Result);
        }

        var plan = preflight.Plan!;
        var target = preflight.Target!;
        // C-IR-07: không in email đích. Operator phải gõ email lấy từ yêu cầu recovery đã được phê duyệt (kênh ngoài CLI), nên
        // bước xác nhận chứng minh hai nguồn độc lập khớp nhau thay vì chép lại giá trị vừa hiển thị.
        console.WriteLine($"Recovery {preflight.Record!.RecoveryId:D} ({plan.Purpose}) authorized for platform account id {target.Id:D}.");
        if (!console.IsInteractive)
        {
            throw new CliUsageException("interactive_terminal_required");
        }

        if (plan.Kind == RecoveryPlanKind.FinalizeOnly)
        {
            console.WriteLine("This recovery was already applied to SQLite; only authority finalization remains. No credential will be changed.");
            if (!Confirm("FINALIZE"))
            {
                console.WriteError("cancelled");
                return CliExitCodes.Cancelled;
            }

            return Report(await service.FinalizeAsync(cancellationToken));
        }

        string? passwordHash = null;
        if (plan.Purpose == ProvisioningRecoveryPurpose.AdminRecovery)
        {
            // Operator xác nhận độc lập đúng tài khoản mà authority đã chỉ định.
            var confirmation = PlatformEmailNormalizer.TryNormalize(arguments.Get("email") ?? console.ReadLine("Target account email from the approved recovery request: "));
            if (!string.Equals(confirmation, target.Email, StringComparison.Ordinal))
            {
                await TryAuditDenialAsync(context, preflight.Inspection, PlatformAuditActions.RecoveryDenied, "recovery_target_confirmation_mismatch", requestId);
                return Report(OperationResult.Denied("recovery_target_confirmation_mismatch"));
            }

            var password = ReadNewPassword();
            if (password is null)
            {
                return CliExitCodes.UsageError;
            }

            passwordHash = new PlatformPasswordHasher().HashPassword(password);
            console.WriteLine("The account will be re-activated with must_change_password=true; all existing platform tokens are revoked.");
        }
        else
        {
            console.WriteLine("Bootstrap reconciliation: credentials are unchanged; existing platform tokens of the account are revoked.");
        }

        if (!Confirm("RECOVER"))
        {
            console.WriteError("cancelled");
            return CliExitCodes.Cancelled;
        }

        return Report(await service.ApplyAsync(passwordHash, requestId, cancellationToken));
    }

    private int? RequireMutationPreconditions(PlatformCliContext context)
    {
        if (context.Authority is null)
        {
            console.WriteError("denied: provisioning_authority_not_configured");
            return CliExitCodes.Denied;
        }

        // C-IR-03: thao tác ghi luôn yêu cầu phiên quản trị nâng quyền trên host, ở MỌI môi trường; tên môi trường (do biến môi
        // trường quyết định) không bao giờ miễn kiểm tra này. Ranh giới ủy quyền thật là Authority Service phía server.
        if (!context.IsPrivilegedOperator)
        {
            console.WriteError("denied: privileged_operator_required (run from an elevated administrator session on the deployment host)");
            return CliExitCodes.Denied;
        }

        if (!context.DatabaseExists)
        {
            console.WriteError("denied: database_not_found");
            return CliExitCodes.Denied;
        }

        return null;
    }

    // Từ chối sau bootstrap được audit (best-effort). Trước bootstrap không ghi gì để không phá điều kiện bootstrap lần đầu.
    private static async Task TryAuditDenialAsync(PlatformCliContext context, PlatformDatabaseInspection? inspection, string action, string reason, string requestId)
    {
        if (inspection is not { SchemaProblem: null, State.BootstrapStatus: PlatformBootstrapStatus.Completed })
        {
            return;
        }

        try
        {
            await using var db = context.CreateDbContext(readOnly: false);
            new PlatformAuditWriter(db).Stage(new PlatformAuditEntry(
                action,
                PlatformAuditOutcome.Denied,
                null,
                requestId,
                new Dictionary<string, string> { [PlatformAuditMetadataKeys.Reason] = reason }));
            await db.SaveChangesAsync();
        }
        catch (Exception)
        {
            // Lệnh đã bị từ chối; không ghi được audit từ chối không làm thay đổi kết quả.
        }
    }

    private string? ReadNewPassword()
    {
        var password = console.ReadSecret("New password: ");
        var confirmation = console.ReadSecret("Confirm new password: ");
        if (password is null || confirmation is null)
        {
            console.WriteError("error: interactive_terminal_required");
            return null;
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            console.WriteError("error: password_confirmation_mismatch");
            return null;
        }

        if (password.Length is < PlatformAuthService.MinPasswordLength or > PlatformAuthService.MaxPasswordLength || string.IsNullOrWhiteSpace(password))
        {
            console.WriteError($"error: password_policy ({PlatformAuthService.MinPasswordLength}-{PlatformAuthService.MaxPasswordLength} characters)");
            return null;
        }

        return password;
    }

    private bool Confirm(string word)
    {
        return string.Equals(console.ReadLine($"Type {word} to confirm: ")?.Trim(), word, StringComparison.Ordinal);
    }

    private int Report(OperationResult result)
    {
        var line = $"result: {result.Outcome} ({result.Reason})";
        if (result.Outcome == OperationOutcome.Success)
        {
            console.WriteLine(line);
            return CliExitCodes.Success;
        }

        console.WriteError(line);
        if (result.Outcome == OperationOutcome.ReconciliationRequired)
        {
            console.WriteError("Operation is NOT complete. Platform authentication remains denied. Run 'recovery-check' and follow the runbook.");
        }

        return result.Outcome switch
        {
            OperationOutcome.AuthorityUnavailable => CliExitCodes.AuthorityUnavailable,
            OperationOutcome.ReconciliationRequired => CliExitCodes.ReconciliationRequired,
            _ => CliExitCodes.Denied,
        };
    }

    private void Field(string name, string value)
    {
        console.WriteLine($"  {name + ":",-30} {value}");
    }

    private static string Count(PlatformDatabaseInspection inspection)
    {
        var admins = inspection.Admins;
        return string.Create(CultureInfo.InvariantCulture,
            $"{admins.Count} (active {admins.Count(a => a.Status == PlatformAdminStatus.Active)}, disabled {admins.Count(a => a.Status == PlatformAdminStatus.Disabled)}, locked {admins.Count(a => a.Status == PlatformAdminStatus.Locked)})");
    }

    private static string Format(DateTimeOffset? value)
    {
        return value is { } time ? AuthorityTimestamp.Serialize(time) : "-";
    }
}
