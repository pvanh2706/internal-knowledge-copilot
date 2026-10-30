using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.PlatformCli.Preflight;

public enum ProvisioningCondition
{
    Ready,
    EligibleForBootstrap,
    NotAuthorized,
    BootstrapActivationPending,
    ReservationWithoutSqliteEvidence,
    RecoveryAuthorized,
    RecoveryAppliedPendingFinalization,
    SqliteRestoreSuspected,
    StateVersionMismatch,
    EvidenceMismatch,
    AnomalousSqliteState,
    AuthorityMissing,
    AuthorityMismatch,
    AuthorityRevoked,
    AuthorityMalformed,
    AuthorityUnavailable,
    DatabaseUnavailable,
    NotConfigured,
}

public sealed record ProvisioningDiagnosis(ProvisioningCondition Condition, string Reason, string Action);

public enum RecoveryPlanKind
{
    Apply,
    FinalizeOnly,
    Reject,
}

public sealed record RecoveryPlan(
    RecoveryPlanKind Kind,
    string Reason,
    ProvisioningRecoveryPurpose? Purpose = null,
    Guid? TargetAdminId = null,
    int ExpectedStateVersion = 0)
{
    public static RecoveryPlan Reject(string reason) => new(RecoveryPlanKind.Reject, reason);
}

// Phân loại tổ hợp (SQLite, authority) thành điều kiện có hành động khuyến nghị. Không bao giờ coi một trạng thái lệch
// là an toàn: mọi trường hợp không phải Ready đều bị runtime từ chối.
public static class ProvisioningReconciliation
{
    public static ProvisioningDiagnosis Diagnose(
        PlatformDatabaseInspection? inspection,
        ProvisioningAuthorityReadResult? read,
        ProvisioningAuthorityIdentity? identity)
    {
        if (read is null || identity is null)
        {
            return new(ProvisioningCondition.NotConfigured, "provisioning_authority_not_configured",
                "Configure ProvisioningAuthority (DynamoDb in production). Platform authentication stays denied.");
        }

        if (inspection is null)
        {
            return new(ProvisioningCondition.DatabaseUnavailable, "database_not_found",
                "Verify Database:SqlitePath and --app-dir. The CLI never creates or migrates the database.");
        }

        if (read.Outcome == ProvisioningAuthorityReadOutcome.Unavailable)
        {
            return new(ProvisioningCondition.AuthorityUnavailable, read.Reason,
                "Restore connectivity/permissions to the provisioning authority and re-run. Platform authentication stays denied.");
        }

        if (inspection.SchemaProblem is { } schemaProblem)
        {
            return new(ProvisioningCondition.AnomalousSqliteState, schemaProblem,
                "Apply the approved migrations with the deployment procedure (the CLI does not migrate) and verify platform triggers.");
        }

        if (read.Outcome == ProvisioningAuthorityReadOutcome.Malformed || read.Record is null)
        {
            return read.Outcome == ProvisioningAuthorityReadOutcome.NotFound
                ? inspection.IsPristine
                    ? new(ProvisioningCondition.NotAuthorized, read.Reason,
                        "Request an Authorized record from the independent provisioning authority before bootstrap.")
                    : new(ProvisioningCondition.AuthorityMissing, read.Reason,
                        "SQLite has platform state but no authority record exists. Escalate to security operations; do not bootstrap.")
                : new(ProvisioningCondition.AuthorityMalformed, read.Reason,
                    "Authority record failed validation/integrity checks. Escalate to the provisioning authority; do not edit it manually.");
        }

        var record = read.Record;
        if (!record.Matches(identity))
        {
            return new(ProvisioningCondition.AuthorityMismatch, "authority_identity_mismatch",
                "Deployment/authority identity differs from configuration. Verify ProvisioningAuthority:DeploymentId/AuthorityId.");
        }

        if (inspection.State is { } state && state.DeploymentId != identity.DeploymentId)
        {
            return new(ProvisioningCondition.EvidenceMismatch, "deployment_identity_mismatch",
                "SQLite belongs to a different deployment. Verify the database file; escalate to security operations.");
        }

        if (record.Status == ProvisioningAuthorityStatus.Revoked)
        {
            return new(ProvisioningCondition.AuthorityRevoked, "authority_revoked",
                "Provisioning authority revoked this deployment. Only the provisioning authority can lift this.");
        }

        if (inspection.Anomaly is { } anomaly)
        {
            return new(ProvisioningCondition.AnomalousSqliteState, anomaly,
                "SQLite platform state is inconsistent. Escalate to security operations; bootstrap and recovery are blocked.");
        }

        switch (record.Status)
        {
            case ProvisioningAuthorityStatus.Authorized:
                return inspection.IsPristine
                    ? new(ProvisioningCondition.EligibleForBootstrap, "eligible", "Run 'bootstrap' as the authorized operator.")
                    : new(ProvisioningCondition.EvidenceMismatch, "sqlite_completed_without_reservation",
                        "SQLite reports a completed bootstrap that the authority never reserved. Escalate; do not re-authorize.");

            case ProvisioningAuthorityStatus.Reserved:
                if (inspection.IsPristine)
                {
                    return new(ProvisioningCondition.ReservationWithoutSqliteEvidence, "reservation_without_sqlite_evidence",
                        "Interrupted before SQLite commit, or SQLite was restored. The reservation does not expire: the provisioning " +
                        "authority must investigate (restore boundary, C-G09) and explicitly issue a fresh authorization.");
                }

                return MatchesReservation(inspection, record) && inspection.State!.StateVersion == 1
                    ? new(ProvisioningCondition.BootstrapActivationPending, "authority_activation_pending",
                        "SQLite bootstrap committed but authority was not activated. Request RecoveryPending " +
                        "(BootstrapReconciliation) from the provisioning authority, then run 'recover-admin'.")
                    : new(ProvisioningCondition.EvidenceMismatch, "reservation_evidence_mismatch",
                        "SQLite bootstrap does not match the authority reservation. Escalate to security operations.");

            case ProvisioningAuthorityStatus.Activated:
                if (inspection.IsPristine)
                {
                    return new(ProvisioningCondition.SqliteRestoreSuspected, "sqlite_restore_suspected",
                        "Authority is activated but SQLite has no bootstrap: a pre-bootstrap database was restored. Bootstrap stays " +
                        "denied; restore the correct database or escalate.");
                }

                var runtime = ProvisioningEvidence.EvaluateRuntime(inspection.State, read, identity);
                return runtime.Verified
                    ? new(ProvisioningCondition.Ready, "verified", "No action required.")
                    : runtime.Reason == "state_version_mismatch"
                        ? new(ProvisioningCondition.StateVersionMismatch, runtime.Reason,
                            "SQLite security state version differs from the authority (older/newer snapshot or out-of-band change). Escalate.")
                        : new(ProvisioningCondition.EvidenceMismatch, runtime.Reason,
                            "SQLite bootstrap evidence differs from the authority. Escalate to security operations.");

            case ProvisioningAuthorityStatus.RecoveryPending:
                if (inspection.IsPristine)
                {
                    return record.RecoveryPurpose == ProvisioningRecoveryPurpose.BootstrapReconciliation
                        ? new(ProvisioningCondition.ReservationWithoutSqliteEvidence, "reservation_without_sqlite_evidence",
                            "No SQLite bootstrap evidence to reconcile. The provisioning authority must decide on a fresh authorization.")
                        : new(ProvisioningCondition.SqliteRestoreSuspected, "sqlite_restore_suspected",
                            "Recovery authorized but SQLite has no bootstrap. Restore the correct database or escalate.");
                }

                var plan = PlanRecovery(inspection, record);
                return plan.Kind switch
                {
                    RecoveryPlanKind.Apply => new(ProvisioningCondition.RecoveryAuthorized, "recovery_authorized",
                        "Run 'recover-admin' as the authorized operator. Platform authentication stays denied until it completes."),
                    RecoveryPlanKind.FinalizeOnly => new(ProvisioningCondition.RecoveryAppliedPendingFinalization, "recovery_finalization_pending",
                        "Recovery was applied to SQLite but not finalized in the authority. Re-run 'recover-admin' to finalize."),
                    _ when plan.Reason == "state_version_mismatch" => new(ProvisioningCondition.StateVersionMismatch, plan.Reason,
                        "SQLite security state version does not match the recovery authorization. Escalate."),
                    _ => new(ProvisioningCondition.EvidenceMismatch, plan.Reason,
                        "SQLite does not match the recovery authorization. Escalate to security operations."),
                };

            default:
                return new(ProvisioningCondition.AuthorityMalformed, "authority_status_unknown", "Escalate to the provisioning authority.");
        }
    }

    // Kế hoạch recovery cho bản ghi RecoveryPending: Apply (chưa áp dụng vào SQLite), FinalizeOnly (đã áp dụng, chỉ còn
    // kích hoạt lại authority) hoặc Reject. Recovery không bao giờ chạy lại bootstrap và không đổi bản ghi Completed.
    public static RecoveryPlan PlanRecovery(PlatformDatabaseInspection inspection, ProvisioningAuthorityRecord record)
    {
        if (record.Status != ProvisioningAuthorityStatus.RecoveryPending || record.RecoveryId is not { } recoveryId)
        {
            return RecoveryPlan.Reject("recovery_not_authorized");
        }

        if (!inspection.IsCompleted)
        {
            return RecoveryPlan.Reject(inspection.SchemaProblem ?? "bootstrap_not_completed");
        }

        if (inspection.Anomaly is { } anomaly)
        {
            return RecoveryPlan.Reject(anomaly);
        }

        var state = inspection.State!;
        int priorStateVersion;
        Guid targetAdminId;
        switch (record.RecoveryPurpose)
        {
            case ProvisioningRecoveryPurpose.AdminRecovery:
                if (state.BootstrapAdminId != record.BootstrapAdminId || !AuthorityTimestamp.AreEqual(state.BootstrapCompletedAt, record.BootstrapCompletedAt))
                {
                    return RecoveryPlan.Reject("bootstrap_evidence_mismatch");
                }

                targetAdminId = record.RecoveryTargetAdminId!.Value;
                priorStateVersion = record.StateVersion!.Value;
                break;

            case ProvisioningRecoveryPurpose.BootstrapReconciliation:
                if (!MatchesReservation(inspection, record))
                {
                    return RecoveryPlan.Reject("reservation_evidence_mismatch");
                }

                targetAdminId = state.BootstrapAdminId!.Value;
                priorStateVersion = 1;
                break;

            default:
                return RecoveryPlan.Reject("recovery_purpose_invalid");
        }

        if (inspection.Admins.All(admin => admin.Id != targetAdminId))
        {
            return RecoveryPlan.Reject("recovery_target_not_found");
        }

        if (inspection.AppliedRecoveryIds.Contains(recoveryId))
        {
            return state.StateVersion == priorStateVersion + 1
                ? new RecoveryPlan(RecoveryPlanKind.FinalizeOnly, "recovery_already_applied", record.RecoveryPurpose, targetAdminId, priorStateVersion)
                : RecoveryPlan.Reject("state_version_mismatch");
        }

        return state.StateVersion == priorStateVersion
            ? new RecoveryPlan(RecoveryPlanKind.Apply, "recovery_authorized", record.RecoveryPurpose, targetAdminId, priorStateVersion)
            : RecoveryPlan.Reject("state_version_mismatch");
    }

    private static bool MatchesReservation(PlatformDatabaseInspection inspection, ProvisioningAuthorityRecord record)
    {
        return inspection.State is { } state
            && record.ReservedAdminId is not null
            && state.BootstrapAdminId == record.ReservedAdminId
            && inspection.BootstrapAuditReservationId == record.ReservationId;
    }
}
