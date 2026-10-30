# S01 — Architecture & Security Review and Design Recheck

**Project:** Enterprise AI Copilot  
**Reviewed design:** S01 Detailed Technical Design v0.2 → remediation v0.3 → [Technical Design Baseline v1.0](S01_TECHNICAL_DESIGN_BASELINE_V1.0.md)  
**PO baseline approval:** 2026-10-30  
**Review outcome:** 9/9 findings have **design-level** mitigation; **0/9 runtime fixes verified**  
**Code reference:** `main` @ `939b32f1a5273ecd6073273fd412074a2d7a835a`  
**Implementation:** NOT AUTHORIZED  

## 1. Method, evidence and limitations

Static analysis of current code and existing Architecture Review AR-B-01–04. No new `dotnet test`, frontend test, live exploit, integration test, SQLite bootstrap concurrency, restore, penetration or production validation has been executed. Labels below reflect **design completeness**, never verified security remediation.

Key observed boundaries:
- `Modules/Tenants/TenantsController.cs`: `[Authorize(Roles = nameof(UserRole.Admin))]` protects platform-wide tenant management.
- `Infrastructure/Tenancy/TenantResolutionMiddleware.cs`: default/header tenant resolution; `tenant_id` consistency is not enforced as a full principal validation contract.
- `Infrastructure/Database/AppDbContext.cs`: null tenant context disables global tenant query filters.
- `Modules/ActionApprovals/ActionApprovalService.cs`: approval lacks sufficient role/SoD policy, rule approval can mark status Approved, execution can call external source.
- `Infrastructure/BackgroundJobs/ProcessingJobService.cs`: `ActionExecution` job invokes service execution.
- `Infrastructure/Connectors/InternalHttpExternalConnector.cs`: outbound `/copilot/actions/execute` HTTP POST.
- `Infrastructure/Audit/AuditLogService.cs`: tenant fallback to default tenant for missing context.
- `Program.cs`: single JWT scheme; `src/frontend/src/router/index.ts` has common `requiresAdmin` guard.

## 2. Findings and remediation decisions

| Finding | Priority | Original gap | Baseline v1.0 design remediation | Design Recheck | Runtime |
|---|---|---|---|---|---|
| S01-R01 | P0 | Null tenant context may bypass query filters | Explicit tenant HTTP fail-closed, separate trusted worker scope, limited platform services | ADDRESSED IN DESIGN | OPEN |
| S01-R02 | P0 | JWT cross-scheme confusion | Distinct schemes, claims, keys/audiences and explicit endpoint policies | ADDRESSED IN DESIGN | OPEN |
| S01-R03 | P0 | External write through API/job/connector | Default-deny at API, service, worker and executor, including legacy jobs | ADDRESSED IN DESIGN | OPEN |
| S01-R04 | P0 | DB-only bootstrap can reopen after restore | Singleton state plus independent provisioning evidence, fail-closed reconciliation | ADDRESSED IN DESIGN | OPEN |
| S01-R05 | P0 | Cutover/rollback could re-enable cross-tenant admin | Security-equivalent rollout/rollback; no legacy admin access window | ADDRESSED IN DESIGN | OPEN |
| S01-R06 | P1 | Approval scopes and self-approval | Tenant RBAC, current role, object and state checks, separation of duties | ADDRESSED IN DESIGN | OPEN |
| S01-R07 | P1 | Platform audit actor conflated with tenant | Platform audit domain, identity, outcome, correlation and durability | ADDRESSED IN DESIGN | OPEN |
| S01-R08 | P1 | Privileged credential/session policy unspecified | Versioned hashing, lockout, restricted password change, account/token revocation | ADDRESSED IN DESIGN | OPEN |
| S01-R09 | P1 | Tests insufficient for new security boundary | HTTP integration + SQLite concurrency + restore/rollback + negative tests | ADDRESSED IN DESIGN | OPEN |

**No finding is marked FIXED/CLOSED.** SEC-01 and SEC-02 from AR-B-02 remain OPEN until separately verified. The decision to accept design is not a waiver of failed/untested security controls.

## 3. Architecture conditions for Implementation Gate

1. No code implementation or database migration without separate PO authorization.
2. Written implementation plan must specify task boundaries, dependencies, acceptance criteria, rollout/rollback and review responsibilities per S01-A..F.
3. Clear fail-closed semantics for unknown tenant, missing ACL/source, invalid claims and bootstrap provenance.
4. No default credentials, automatic platform elevation or production platform exposure without privileged MFA readiness.
5. Full deny of outbound business writes across API/service/worker/connector and inspection of side effects on validation APIs.
6. Secure rollback must not restore Tenant Admin global privileges or old action execution.
7. Test evidence must be produced after implementation; static review alone is insufficient.

## 4. Out-of-scope and residual risks

S02 owns SEC-03–07 external source permission revalidation and connector trust. S03–S05 own remaining reliability, consistency and operational risks. Confirmed safe API behavior, detailed hash settings, CLI recovery logistics, secrets and MFA implementation require later verified operational decisions; this report does not certify production security.

## 5. Governance

- S01-Q01–Q04: **APPROVED** by PO.
- S01 Technical Design Baseline v1.0: **APPROVED** by PO, 2026-10-30.
- R01–R09: **ADDRESSED IN DESIGN; IMPLEMENTATION/VERIFICATION OPEN**.
- Publication: documentation-only PR, merge subject to explicit PO approval.
- Implementation: **NOT AUTHORIZED**.
