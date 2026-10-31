# S01-C — Bootstrap CLI, Provisioning Authority and Recovery
## Implementation Notes, Runbook and Gate Evidence

**Package:** S01-C (Workstream 01 — Identity & Authorization Hardening)
**Authorization:** C-EX1 Execution Authorization v1.0 (C1–C6, isolated verification, commit, push, PR). C-EX2 (AWS infrastructure evidence), C-EX3 (merge/acceptance) and production deployment are **NOT AUTHORIZED** by this work.
**Base:** `main` @ `9b9bf5bad7733ecb7eef8e7a8d9cf60ef2fcb3b3`
**Verification level:** automated tests on real SQLite files and real OS processes; DynamoDB exercised **only through in-memory test doubles**. No AWS resource was created or called.

> IMPLEMENTED ≠ VERIFIED IN PRODUCTION. **C-G09 (privileged-state rollback) remains OPEN and production-blocking.**

> **S01-C-REM (security remediation R1–R6) supersedes parts of this document.** The operator no longer holds any DynamoDB write path: authority changes go through the server-side Authority Service (API Gateway IAM auth → Approval/Transition handlers). Sections marked *SUPERSEDED* below (DynamoDb operator mutations in §2, the DynamoDb runbook rows in §6 and the IAM assumptions in §7) are kept for history only. See [S01C_SECURITY_REMEDIATION_R1_R6.md](S01C_SECURITY_REMEDIATION_R1_R6.md).

---

## 1. Components

| Assembly | Role | Contains authority write code? |
|---|---|---|
| `InternalKnowledgeCopilot.Api` | Runtime. `ProvisioningAuthorityVerifier` (implements S01-B `IPlatformProvisioningVerifier`) reads evidence on **every** platform login / platform JWT validation. Readers: `LocalSealProvisioningAuthorityReader`, `DynamoDbProvisioningAuthorityReader` (GetItem, `ConsistentRead=true`). | **No** (verified by test) |
| `InternalKnowledgeCopilot.PlatformCli` | Restricted operator CLI (`Microsoft.NET.Sdk` console, no `WebApplication`/`IHost`): `status`, `recovery-check`, `bootstrap`, `recover-admin`. Owns the only authority mutators. | Yes (operator-only) |
| `InternalKnowledgeCopilot.PlatformCli.TestHarness` | **Test/dev only, not deployable.** Runs the real CLI with scripted answers (multi-process tests) and simulates the *independent* provisioning authority for LocalSeal. Refuses to run outside Development/Testing. | Simulator only (LocalSeal) |

No HTTP endpoint, middleware, hosted service, seeder or migration was added. The CLI never migrates or creates the database (`Mode=ReadWrite` never creates a file; read-only commands open `Mode=ReadOnly`).

## 2. Provisioning authority model

One authority record per deployment, outside the SQLite restore boundary.

```
          (independent authority)            (operator CLI, conditional)
 [none] ─────────────────────────► Authorized ──reserve──► Reserved ──activate──► Activated
                                                               │                     │
                         (independent authority) RecoveryPending(BootstrapReconciliation)
                                                               │      (independent) RecoveryPending(AdminRecovery)
                                                               ▼                     ▼
                                                        recover-admin ─finalize─► Activated (last_recovery_id, state_version+1)
 Any state ──(independent authority)──► Revoked
```

* CLI-permitted transitions are enforced in code (`ProvisioningAuthorityTransitions.EnsureOperatorTransition`): only `Authorized→Reserved`, `Reserved→Activated`, `RecoveryPending→Activated`, version exactly +1, identity/authorization fields unchanged. The CLI has **no** code path that creates `Authorized`, `RecoveryPending` or `Revoked`.
* A reservation **never expires**. Re-opening bootstrap after an interrupted reservation requires an explicit fresh authorization by the independent authority.
* Record fields: `deployment_id`, `authority_id`, `status`, `version`, `authorization_id`, `authorized_at`, `reservation_id`, `reserved_admin_id`, `reserved_at`, `bootstrap_admin_id`, `bootstrap_completed_at`, `activated_at`, `state_version`, `recovery_id`, `recovery_purpose`, `recovery_target_admin_id`, `recovery_authorized_at`, `last_recovery_id`, `last_recovered_at`, `recovery_count`. Structural validity per status is checked on every read (`ProvisioningAuthorityRecord.Validate`); malformed ⇒ fail closed.

### Runtime rule (single source: `ProvisioningEvidence.EvaluateRuntime`)

Platform authentication is allowed only when **all** hold: record found and well-formed; `deployment_id`/`authority_id` equal configuration; status `Activated`; SQLite `platform_security_state` is `Completed`; SQLite `deployment_id` equals configuration; `bootstrap_admin_id` and `bootstrap_completed_at` (ms precision) equal the authority; `state_version` equal. Anything else (missing, revoked, reserved, recovery pending, timeout, access denied, throttled, malformed, mismatch) ⇒ deny. No cache, no bypass, no fallback provider.

### Providers

| Provider | Environments | Notes |
|---|---|---|
| `None` (default) | all | S01-B behavior: platform auth always denied. |
| `LocalSeal` | **Development/Testing only** (startup/CLI refuse otherwise) | JSON envelope with HMAC-SHA256 over the payload (key ≥ 32 bytes from config); atomic replace; exclusive `.lock` file for read-modify-write. Must not live inside the SQLite directory. **Rollback limitation:** a full-host restore, or anyone who can read the integrity key, can restore/forge the seal. Never a production fallback. |
| `DynamoDb` | production target | *SUPERSEDED (S01-C-REM): operator `UpdateItem` removed; writes only by the Authority Service.* Item key `pk = "deployment#<deployment_id>"`. Runtime: `GetItem` + `ConsistentRead=true` on the base table, timeout (1–30 s, default 3 s), 1 SDK retry, errors mapped to safe reason codes. Operator: `UpdateItem` with `ConditionExpression = attribute_exists(pk) AND status AND version AND deployment_id AND authority_id [AND reservation_id] [AND recovery_id]`; never `PutItem`. Mutations require an explicit `OperatorProfile` and never fall back to the default credential chain. |

Configuration (`ProvisioningAuthority` section): `Provider`, `DeploymentId`, `AuthorityId`, `LocalSeal:{Path,IntegrityKey}`, `DynamoDb:{TableName,Region,TimeoutSeconds,OperatorProfile}`. There are no boolean/bypass options (test-enforced). AWS credentials are never read from appsettings.

## 3. Bootstrap (C2, C4)

Preflight (read-only): DB file exists; applied migrations exactly equal the CLI's migrations (no pending/unknown); the five S01-A platform triggers exist; no `platform_security_state` row; zero platform admins; zero platform audit rows; authority `Authorized` with matching identity. Any deviation ⇒ exit 2, nothing written (pre-bootstrap denials are deliberately not audited, consistent with S01-B).

Sequence:
1. Verify `Authorized` (re-read immediately before reserving).
2. Conditional reserve (pre-generates `reservation_id` and the admin id).
3. SQLite transaction (`BEGIN IMMEDIATE`): re-check eligibility inside the transaction, insert exactly one admin (Argon2id hash from S01-B hasher, `must_change_password=true`, `token_version=0`, `Active`), the singleton `Completed` state (`state_version=1`), and the mandatory `platform.bootstrap.completed` audit (metadata: reservation id, deployment id, state version, provider).
4. Commit.
5. Conditional activate (`bootstrap_admin_id`, `bootstrap_completed_at`, `state_version=1`).
6. Re-read both stores and apply the runtime rule. Exit 0 only if verified.

| Failure point | Resulting state | Runtime | CLI exit | Resolution |
|---|---|---|---|---|
| Before/at reserve (unavailable, not applied) | unchanged | deny | 3 | retry |
| Reserve lost race | unchanged | deny | 2 | none |
| SQLite transaction fails (incl. mandatory audit failure) | SQLite rolled back, authority `Reserved` | deny | 4 | authority re-authorizes after investigation |
| Crash/failure after commit, before activation | SQLite `Completed`, authority `Reserved` | deny | 4/5 | `RecoveryPending(BootstrapReconciliation)` → `recover-admin` |
| Activation applied but response lost | consistent | allow | 0 (confirmed by re-read) | none |

## 4. Recovery and reconciliation (C5)

`recover-admin` runs only under an independent `RecoveryPending` record for this deployment:

* **AdminRecovery** (target admin id bound in the authority record): operator re-types the target email (must match) and enters a new password twice (no echo). One SQLite transaction: new Argon2id hash, `must_change_password=true`, `Active`, failed count 0, lock cleared, `token_version+1`, `state_version+1`, audits `platform.recovery.admin_recovered` + `platform.auth.tokens_revoked`. Then conditional `RecoveryPending→Activated` with `state_version`, `last_recovery_id`, `recovery_count+1`.
* **BootstrapReconciliation**: requires SQLite bootstrap evidence matching the reservation (admin id + audit reservation id + `state_version=1`). No credential change; `token_version+1`; `state_version=2`; audit `platform.recovery.bootstrap_reconciled`; authority activated with the SQLite completion evidence.

Guarantees: bootstrap is never re-run; `Completed` fields are never modified (S01-A trigger); no audit row is deleted/rewritten (append-only trigger); old JWTs fail `token_version` validation; during `RecoveryPending` all platform authentication is denied. Duplicate/concurrent runs are serialized by `BEGIN IMMEDIATE` and detected by the recovery audit (`recovery_id`); an interrupted run (applied in SQLite, not finalized) is resumed by re-running `recover-admin`, which **only finalizes** (`FINALIZE`) and never re-applies a credential.

`state_version` binds SQLite to the authority: restoring a Completed snapshot older than the last recovery is detected (`state_version_mismatch`). Restoring a snapshot with the **same** `state_version` is **not** detected (C-G09).

## 5. CLI reference (C1)

```
InternalKnowledgeCopilot.PlatformCli <status|recovery-check|bootstrap|recover-admin> [--app-dir <api dir>] [--email <e>] [--display-name <n>]
```

* Passwords are only read interactively without echo; any option containing `password|passwd|pwd|secret|token|credential|key|hash` and any positional argument are rejected (exit 1) without echoing values. Redirected stdin is refused for credential prompts.
* Explicit typed confirmations: `BOOTSTRAP`, `RECOVER`, `FINALIZE`.
* Outside Development/Testing, mutating commands require an elevated process (`Environment.IsPrivilegedProcess`).
* Output contains reason codes, ids and statuses only — never passwords, hashes, tokens, integrity keys or raw AWS/SQLite error text.
* Exit codes: `0` success/ready · `1` usage · `2` denied/not ready (no change) · `3` authority unavailable · `4` reconciliation required (partial; auth stays denied) · `5` internal error · `6` cancelled · `7` configuration invalid.
* Configuration: `appsettings.json`, `appsettings.{Environment}.json` from `--app-dir` (use the **API directory** in production; the build output also contains a copy of the API appsettings), then environment variables. Environment: `ASPNETCORE_ENVIRONMENT` → `DOTNET_ENVIRONMENT` → `Production`. Relative `Database:SqlitePath` resolves against `--app-dir`.

## 6. Recovery / reconciliation runbook

*For the DynamoDb provider this runbook is SUPERSEDED by §8 of [S01C_SECURITY_REMEDIATION_R1_R6.md](S01C_SECURITY_REMEDIATION_R1_R6.md) (approver/operator separation through the Authority Service). It remains valid for LocalSeal (Development/Testing only).*

Always start with `recovery-check` (read-only). Act on `Condition`:

| Condition | Meaning | Action |
|---|---|---|
| `Ready` | verified | none |
| `NotAuthorized` | no record, pristine DB | request `Authorized` from the provisioning authority |
| `EligibleForBootstrap` | `Authorized`, pristine DB | run `bootstrap` (elevated, interactive) |
| `BootstrapActivationPending` | SQLite committed, authority `Reserved` | authority issues `RecoveryPending(BootstrapReconciliation)`; run `recover-admin`, type `RECOVER` |
| `ReservationWithoutSqliteEvidence` | `Reserved`, pristine DB | security ops confirm no DB restore / wrong DB path; authority may issue a **fresh** `Authorized`; then `bootstrap` |
| `RecoveryAuthorized` | `RecoveryPending` valid | run `recover-admin --email <target>` |
| `RecoveryAppliedPendingFinalization` | interrupted recovery | re-run `recover-admin`, type `FINALIZE` |
| `SqliteRestoreSuspected` | authority activated, DB pre-bootstrap | restore the correct DB backup; never re-authorize bootstrap without investigation |
| `StateVersionMismatch` / `EvidenceMismatch` / `AnomalousSqliteState` / `AuthorityMissing` / `AuthorityMismatch` / `AuthorityMalformed` | inconsistent stores or tampering | escalate to security operations; do not edit records manually |
| `AuthorityRevoked` | revoked | only the provisioning authority can lift it |
| `AuthorityUnavailable` | timeout / access denied / throttled | restore connectivity/IAM; platform auth stays denied meanwhile |

After every bootstrap or recovery the account receives a restricted password-change session at first login (S01-B).

## 7. DynamoDB / IAM design assumptions (for C-EX2 — NOT executed)

*SUPERSEDED (S01-C-REM, finding C-IR-01): the "Operator role: GetItem + UpdateItem" assumption below is unsafe — `UpdateItem` without a condition upserts and IAM cannot constrain values or state transitions. Replacement templates: `src/backend/InternalKnowledgeCopilot.AuthorityService/Policies/*.json`; operator and approver roles hold no DynamoDB write permission.*

* Table: on-demand or provisioned, partition key `pk` (S), no sort key, point-in-time recovery and deletion protection enabled, SSE with a KMS key; single authoritative region (strongly consistent reads are only defined against the table's own region; Global Tables require a separate decision).
* **Runtime role** (instance/app-pool identity): `dynamodb:GetItem` only, on the table ARN, with `dynamodb:LeadingKeys = ["deployment#<id>"]`.
* **Operator role** (assumed by the deployment operator with MFA via `OperatorProfile`): `dynamodb:GetItem`, `dynamodb:UpdateItem` on the same item (LeadingKeys). No `PutItem`, `DeleteItem`, `Scan`, `Query`, table management.
* **Provisioning authority role** (separate team/account boundary): creates `Authorized`, sets `RecoveryPending`/`Revoked`, re-authorizes; not usable by the deployment host.
* CloudTrail data events on the table for all three roles.
* Must be verified in C-EX2: IAM denials per role, `ConsistentRead` behavior, conditional-write semantics, timeout/throttling behavior, credential sourcing (`OperatorProfile` incl. assume-role via `AWSSDK.SecurityToken`), endpoint/region configuration and latency impact of per-request reads.

## 8. Gate traceability

| Gate | Result | Direct evidence |
|---|---|---|
| C-G01 No public bootstrap / default admin / auto-seed | **PASS (code/tests)** | `Api_ExposesNoHttpBootstrapOrRecoveryEndpoint_AndContainsNoAuthorityMutationCode`, `Api_PostToBootstrapLikeRoutes_NeverCreatesPlatformAdmin`, `Bootstrap_PendingMigrations_IsDeniedAndCliNeverMigrates`, `Bootstrap_MissingDatabase_IsDeniedAndDatabaseIsNotCreated`, `CliProcess_StatusExitsOnItsOwnWithoutHostingOrMigrating`; S01-A `Migrate_CleanDatabase_CreatesPlatformSchemaWithoutAccountsOrBootstrapState` |
| C-G02 Runtime read-only authority | **PASS (code/test doubles); AWS IAM NOT VERIFIED** | `RuntimeReader_OnlyIssuesStronglyConsistentGetItemOnBaseTable`, `EndToEnd_DynamoDbTestDouble_RuntimeUsesReadOnlyConsistentReads`, `ReadOnlyCommands_DoNotMutateDatabaseOrAuthority`, API assembly has no mutator types |
| C-G03 Atomic SQLite admin/state/audit | **PASS** | `Bootstrap_ValidFirstRun_*` (LocalSeal + DynamoDB double), `Bootstrap_FailureBeforeSqliteCommit_MandatoryAuditFails_*` |
| C-G04 Concurrent bootstrap safety | **PASS** | `ConcurrentBootstrap_InProcess_*` (both providers), `ConcurrentBootstrap_MultipleOsProcesses_CreatesExactlyOneAdmin` (4 OS processes), `ConcurrentBootstrap_WithMisbehavingAuthority_SqliteStillCommitsAtMostOneAdmin`, `ConcurrentRecovery_AppliesExactlyOnceAndRevokesTokensOnce` |
| C-G05 Failure and mismatch fail closed | **PASS (code/test doubles)** | `Bootstrap_MissingRevokedMismatchedOrTamperedAuthority_*` (8 cases), `RuntimeReader_InfrastructureFailures_FailClosedAsUnavailable` (5), `Reader_MalformedOrInconsistentItems_FailClosed` (6), `Bootstrap_FailureAfterSqliteCommitBeforeActivation_*`, `Bootstrap_ProcessCrashBetweenCommitAndActivation_*`, `Runtime_AuthorityRevokedOrTampered_DeniesPlatformAuthentication`, `Recovery_AuthorityMismatch_FailsClosedWithoutChanges` (4) |
| C-G06 Restore cannot reopen bootstrap | **PASS for pre-bootstrap restore and disable/delete; partial for Completed restore** | `PreBootstrapSqliteRestore_CannotReopenBootstrap`, `DisablingAllAdminsOrAttemptingDeletion_NeverReopensBootstrap`, `CompletedSnapshotOlderThanLastRecovery_IsDetectedByStateVersion`; limitation asserted by `CompletedSnapshotWithSameStateVersion_IsNotDetected_DocumentedLimitation` |
| C-G07 Authorized and audited recovery | **PASS** | `AdminRecovery_RequiresIndependentAuthorizationAndOperatorConfirmation`, `AdminRecovery_ResetsCredentialRevokesTokensAuditsAndKeepsCompletedStateImmutable`, `InterruptedRecovery_*`, `RecoverySqliteFailure_*`, `EndToEnd_LocalSeal_*` (HTTP token revocation) |
| C-G08 Security, regression and recovery evidence | **PASS for isolated scope** | full solution: 357 passed / 0 failed / 0 skipped (baseline 265 + 92 S01-C); AWS integration evidence pending C-EX2 |
| C-G09 Privileged-state rollback | **OPEN — production-blocking** | same-`state_version` Completed snapshot and full-host rollback of LocalSeal are undetectable; SQLite write access can alter admin rows without changing `state_version` |

## 9. Open risks

| ID | Risk | Status / mitigation |
|---|---|---|
| C-R01 | Privileged-state rollback: restoring a Completed SQLite snapshot with the same `state_version` (or tampering with admin rows given DB write access) revives earlier account state; LocalSeal shares the host rollback boundary. | OPEN (C-G09). `state_version` binding detects rollback across recovery epochs only. |
| C-R02 | AWS behavior unverified: IAM policies, strong consistency, conditional writes, credential sourcing and timeouts tested only with test doubles. | OPEN until C-EX2. |
| C-R03 | Availability: every platform login/JWT validation performs a DynamoDB GetItem; outage/throttling denies all platform administration (fail closed, by design; no cache). | ACCEPTED-BY-DESIGN pending PO; measure in C-EX2. |
| C-R04 | Operator host trust: elevation check is host-level only; interactive passwords live in managed strings (not zeroized); DB file ACLs and app config protection are operational controls. | OPEN (operational hardening, MFA for privileged access remains an S01 production prerequisite). |
| C-R05 | Independent provisioning authority procedures (create `Authorized`, `RecoveryPending`, `Revoked`, re-authorization after `ReservationWithoutSqliteEvidence`) are not implemented in code for production and depend on a separately controlled process. | OPEN; define in C-EX2 with separation of duties. |

## 10. Known limitations and deviations (for PO review)

1. S01-C planning documents (including the definitions of C-R01–C-R05) are not published in the repository; risk IDs above were defined by the implementer and need PO confirmation.
2. New dependencies: `AWSSDK.DynamoDBv2 4.0.100` (API) and `AWSSDK.SecurityToken 4.0.100` (CLI). AWS SDK v3 is out of support.
3. Two new projects (CLI and a non-deployable TestHarness); the TestHarness also hosts a LocalSeal-only simulator of the independent authority for local development.
4. `PlatformAuditEntry` gained an optional `TargetPlatformAdminId`; allowlists extended with 5 actions (`platform.bootstrap.completed|denied`, `platform.recovery.admin_recovered|bootstrap_reconciled|denied`) and 6 metadata keys (ids/codes only). Post-bootstrap denials are audited best-effort.
5. Recovery re-enables/resets an **existing** platform account only; creating additional platform admins is left to S01-D. Recovery increments `platform_security_state.state_version` (allowed by the S01-A trigger; Completed fields unchanged). No schema or migration change.
6. Test helper `S01BApiFactory` gained `DatabasePath` and a `ConfigureServices` hook (test code only).
7. `PROJECT_STATUS.md` / decision records were not modified (governance documents owned by the PO).
