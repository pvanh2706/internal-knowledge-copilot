# S01 — Identity & Authorization Hardening
## Technical Design Baseline v1.0

**Project:** Enterprise AI Copilot (primary product: Internal Knowledge Copilot B)  
**Decision status:** **APPROVED** by Product Owner, 2026-10-30  
**Documentation publication:** Proposed by this documentation-only PR; merge requires separate PO approval  
**Implementation:** **NOT AUTHORIZED**  
**Reviewed code baseline:** `main` @ `939b32f1a5273ecd6073273fd412074a2d7a835a`  
**Validation level:** Static review and design recheck only; **no code/tests/deployment performed**  

**Decision authority:** Product Owner. This baseline incorporates [S01-Q01–Q04](../decisions/DEC-0008.md), [DEC-0004](../decisions/DEC-0004.md), [DEC-0005](../decisions/DEC-0005.md), [DEC-0006](../decisions/DEC-0006.md) and the [S01 Design Review/Recheck](S01_ARCHITECTURE_SECURITY_REVIEW.md).

> APPROVED design does not mean IMPLEMENTED, VERIFIED or production-ready. SEC-01/SEC-02 remain OPEN until tested remediation and separate PO acceptance.

### 1. Goal, scope and exclusions

S01 addresses AR-B-02 **SEC-01** (platform vs tenant administration) and **SEC-02** (action approval/execution authorization). Establish independent platform identity, strict tenant isolation, auditable administration and **default-deny external business writes** for initial read-only deployment.

**In scope:** platform/tenant authentication boundary; authorization policies; metadata schema; restricted first-admin bootstrap; platform APIs/frontend access separation; action approval authorization; API/worker/executor write lockdown; schema cutover, rollback, tests.

**Out of scope:** SEC-03–SEC-07 full source ACL freshness/revalidation, connector credential handling, ingress protection and SSRF controls (**S02**); reliable job claims (**S03**); index/backup consistency (**S04**); enterprise operational hardening (**S05**); native Jira/CRM/ERP adapters. Fail closed on unsupported external rights; S01 cannot assert completion of the whole Security Gate.

### 2. Approved decisions and logical boundary

| ID | Approved choice | Consequence |
|---|---|---|
| S01-Q01 | **B:** Separate Platform Identity | Platform account is not a tenant `UserEntity` or a new `UserRole` value |
| S01-Q02 | **A:** Local Authentication | Local platform credential flow, separable from tenant auth; OIDC/SSO is future extension |
| S01-Q03 | **A:** Restricted CLI Bootstrap | No public first-admin bootstrap API, no production default credentials |
| S01-Q04 | **A:** Dedicated state table | `platform_security_state` independently tracks one-time bootstrap |

Retain modular monolith (ASP.NET Core/.NET 8 currently), EF Core + SQLite metadata, Vue 3 UI. Platform and tenant identities may share infrastructure/database but **not authorization scope or principal trust**.

### 3. Identity, claims and access rules

Two explicit authentication schemes, `TenantJwt` and `PlatformJwt`, with distinct issuers, audiences, signing credentials and a verified `principal_type`. Platform principal `sub` refers to `platform_admins.id` and carries **no** `tenant_id` / `tenant_code`; tenant principal `sub` refers to `users.id` and carries valid tenant ID + code.

1. Tenant endpoints explicitly require `TenantJwt` and a tenant-scoped authorization policy; platform endpoints require `PlatformJwt` and `PlatformAdminOnly`.
2. A Platform JWT plus `X-Tenant-Code` cannot become tenant identity. A tenant `Admin` token is never platform-authorized. No implicit default-scheme that accepts both on privileged endpoints.
3. Reject invalid/missing/mismatched tenant claims, inactive tenant, wrong audience/issuer/signature, legacy ambiguous claims, stale/disabled principal and revoked privileged tokens. Account status, effective role and `token_version` must be rechecked for privileged operations.
4. Existing `UserRole.Admin` remains **Tenant Admin**, and `/api/users` cannot create/update platform identity. No automatic legacy-admin promotion.
5. Tenant HTTP requests without verified tenant context **fail closed**; remove header/default tenant inference on authenticated tenant API paths. Anonymous tenant login may use explicitly validated tenant selection but cannot infer a privileged scope.
6. Current `AppDbContext` filter permits unfiltered access when context is null; that is **not** an authorization policy. Introduce explicit trusted infrastructure/workload scope for workers/seeding and explicitly tenant-scoped repositories/services for tenant requests. Platform service accesses only approved platform data or audited global tenant lifecycle operations; `IgnoreQueryFilters`/cross-tenant reads must not be available to ordinary request paths.
7. Worker execution scope is created from trusted job metadata and verifies tenant/application identity, not an HTTP header. Service-level authorization is mandatory; controller and UI checks alone are insufficient.

### 4. Data model (logical contract; final EF mappings subject to implementation review)

**`platform_admins`:** `id` (PK UUID); `email_normalized` (unique); `display_name`; `password_hash` (versioned, salt and parameters); `status` (active/disabled/locked semantics); `failed_login_count`; `locked_until`; `must_change_password`; `token_version`; `last_login_at`; `created_at`; `updated_at`. **No `tenant_id` or FK to tenant `users`.** Preserve audit history; do not silently hard-delete accounts.

**`platform_audit_logs`:** `id`; `actor_platform_id` (nullable for unauthenticated/security/CLI events); `action`; `target_tenant_id` (nullable); `target_type`/`target_id`; `outcome` (success/denied/failed); `request_id`; `occurred_at`; safe `metadata_json`. Platform audit must not fall back to default tenant. Protect logs from unauthorized modification and secret leakage. For privileged mutations, event persistence and mutation must have an auditable atomic or durable outbox-style guarantee; fail closed if required audit is unavailable.

**`platform_security_state`:** **approved dedicated singleton table**. Proposed columns `id` (enforce singleton), `deployment_id`, `bootstrap_status` (NotStarted/Completed), `bootstrap_admin_id`, `bootstrap_completed_at`, `state_version`, `created_at`, `updated_at`. Enforce unique singleton and state transition invariants through DB transaction/constraints; SQLite concurrency tokens are application-managed, not SQL Server rowversion.

The table prevents implicit re-bootstrap after admin disable/removal **within a valid current DB**. It **cannot alone** prevent a restored pre-bootstrap snapshot from resetting history.

### 5. Local authentication, account security and token contract

- Platform login returns generic invalid-credentials on failure; rate limit and lock out repeated attempts without enabling easy enumeration; audit security events safely.
- Versioned password hashing with upgradeable parameters and a documented tested work factor (existing tenant PBKDF2 parameters are not automatically accepted for new privileged accounts).
- Separate platform signing key in protected configuration, not source; short platform JWT lifetime; validate expiry, token/account version and principal status for privileged requests.
- On first login with `must_change_password=true` issue **restricted** credential-change session, not full `PlatformAdminOnly`. Require password change before platform administrative operations.
- MFA, privileged recovery/session policy and secret management require their own readiness verification before internet-exposed production platform administration; local password selection does not waive them.
- Exact password work factor, lockout thresholds, JWT TTL, MFA implementation and session persistence are implementation/operational parameters needing evidence and review before release.

### 6. Proposed API contract and frontend separation

| API | Auth |
|---|---|
| `POST /api/platform/auth/login` | Anonymous, rate-limited |
| `GET /api/platform/auth/me` | PlatformJwt |
| `POST /api/platform/auth/change-password` | PlatformJwt, restricted change-password session permitted |
| `GET/POST /api/platform/tenants` | PlatformAdminOnly |
| `GET/PATCH/DELETE /api/platform/tenants/{id}` | PlatformAdminOnly |
| `/api/auth/login` and existing tenant auth | Tenant flow |
| `/api/users` and tenant knowledge/application APIs | TenantJwt + tenant-scoped policy |

Legacy `/api/admin/tenants` must **immediately** cease accepting tenant `Admin` during cutover; use canonical `/api/platform/tenants`. Any temporary alias must require `PlatformAdminOnly` and be separately deprecated.

Frontend: separate platform/tenant sessions and router guards (`requiresPlatformAdmin` versus `requiresTenantAdmin`), login/password-change views, navigation and action controls. Token storage and HTTP header handling must not mix identities. Backend enforcement is authoritative, never trust UI guards.

### 7. First-admin Bootstrap CLI — secure invariants

1. CLI is only invoked by an authorized operator on the deployment; **not** an HTTP bootstrap endpoint or an auto-start seeder. Never expose credentials in CLI arguments, logs or default configuration; use protected interactive input.
2. Verify an eligible fresh deployment, database schema, singleton state, and provisioning authorization **outside the DB snapshot** (e.g. protected deployment provisioning seal / secret-store record). No secrets in the state table.
3. Use a SQLite transaction/unique constraints so first admin, Completed state and success audit are consistent; concurrent CLI invocations cannot create two first admins.
4. External provisioning seal and SQLite transaction are **not one atomic transaction**: interrupted/mismatched states **deny by default** and require manual reconciliation. Do not treat a local seal as immune to full-machine rollback.
5. Record completion durably, mark provisioning evidence consumed, require initial password change. Repeat bootstrap (including after account disable/deletion) is denied.
6. Recovery from loss of all admins is a **separate authorized recovery procedure** with operator verification and audit, never automatic re-opening of bootstrap.
7. Restoring a pre-bootstrap DB (or whole deployment) requires independent evidence outside the affected restore boundary. If unavailable/ambiguous, deny bootstrap pending security operations review.

### 8. Action approval and default-deny external execution

| Operation | Initial S01 policy |
|---|---|
| View own authorized action | Scoped tenant user + verified object permissions |
| List tenant-wide approvals | Tenant Admin/Reviewer + authorized object scope |
| Create draft | Tenant user with scope |
| Approve/reject | Authorized Tenant Admin or Reviewer; current role/resource/state checked |
| Self-approve | **DENY**, even if actor is Admin/Reviewer |
| Rule auto-approve | **DISABLED** in read-only mode |
| Cancel | Only permitted requester/privileged actor with valid state and scope |
| Execute outbound business write | **DENY** for **all principals** in initial read-only mode |

Enforce at controller/API, application service, `ProcessingJobService` `ActionExecution` dispatch, and shared `IExternalActionExecutor`/connector outbound write boundary. Existing Approved/Failed requests and queued jobs are never silently executed; use auditable terminal/blocked handling, **not** infinite retry or false Succeeded status. No worker bypass or user-controlled override of the read-only policy.

Inspect any external `/actions/validate`, permission-check or other POST API for **side effects**: HTTP method/name alone does not establish read-only safety. Until source API behavior is verified, fail closed. Comprehensive source ACL authority/freshness is finalized in S02.

### 9. Platform audit and monitoring

Audit login success/failure, lockout, denial of privileged operations, tenant lifecycle, bootstrap and recovery, account status changes, approval/denial, read-only execution blocks and tenant/principal mismatch. Include principal type, target scope, timestamp, outcome and correlation ID. Never record password, signing keys, raw tokens or sensitive payload. Restricted visibility and integrity checks are required. Platform audit is separate from tenant `audit_logs`.

### 10. Migration and security cutover

| Step | Deliverable / gate |
|---|---|
| M1 — Preflight | Inventory tenant admins, tokens, queued legacy action jobs; backup **and tested restore** |
| M2 — Additive schema | New platform tables/constraints; preserve existing users, roles, audit and knowledge |
| M3 — Identity policies | Dual JWT schemes, strict tenant context, cross-scheme negative tests |
| M4 — Bootstrap | CLI, state transaction, protected provisioning evidence and recovery drill |
| M5 — Tenant platform cutover | Replace global admin authority, platform APIs and frontend separation without legacy security window |
| M6 — Action boundary | Disable auto-approval and all outbound writes (HTTP/service/worker/connector) |
| M7 — Verification | HTTP integration, SQLite concurrent migration/bootstrap, negative security, regression and rollback tests |

Security-sensitive deployments must not expose old global tenant admin routes during an incomplete cutover. Rollback may retain additive tables/audit but must never restore insecure cross-tenant authority or reactivate external writes. Use a security-equivalent prior build, deny controls or verified restoration plan. No automatic down-migration deleting audit.

### 11. Acceptance matrix (minimum)

| ID | Mandatory acceptance evidence |
|---|---|
| ID-01 | Tenant JWT cannot manage platform tenants; platform JWT cannot call tenant APIs |
| ID-02 | Wrong issuer/audience/signature, legacy ambiguous claims, forged scopes rejected |
| ID-03 | Missing/inactive/mismatched tenant ID/code denied; HTTP null context cannot leak all tenants |
| ID-04 | Tenant Admin cannot grant platform privilege; revoked platform account/token loses authority |
| ID-05 | Authenticated platform principal cannot access unauthorized tenant business entities through global DbContext |
| BOOT-01 | First-run creates exactly one admin/state/audit; repeated and concurrent attempts denied |
| BOOT-02 | Partial failure before/after DB commit and external seal mismatch fail closed |
| BOOT-03 | Last-admin loss, DB restore and full deployment restore do not silently reopen bootstrap |
| AUD-01 | Allowed/denied privileged operations logged with correct actor/scope; no default tenant attribution |
| ACT-01 | User cannot approve; requester cannot self-approve; current privilege/state checked |
| ACT-02 | Rule auto-approval disabled for read-only mode |
| ACT-03 | Zero outbound writes from HTTP, service, worker, queued legacy jobs and connector |
| ACT-04 | Unverified side-effectful validation calls are not made |
| MIG-01 | SQLite migration/rollback drill preserves tenant users/roles/data and audit |
| REG-01 | Existing tenant login, user/team, knowledge/RAG and frontend flows pass regression tests |

Run SQLite/provider-real concurrency and HTTP integration tests; EF Core InMemory tests alone do not prove DB constraints or transaction semantics. Record test commands, evidence, defects and remediation decisions; no fresh runtime tests are asserted by this document.

### 12. Implementation packages and independent approval

- **S01-A:** platform schema, migration and platform audit foundations.
- **S01-B:** dual authentication, local platform login, status/revocation and policies.
- **S01-C:** restricted CLI bootstrap, provenance and recovery controls.
- **S01-D:** platform tenant APIs, tenant scope hardening and frontend migration.
- **S01-E:** action permission transitions and complete outbound write lockdown.
- **S01-F:** integration/negative tests, SQLite concurrency, migration and rollback evidence.

Packaging does not authorize coding; dependencies and safe cutover must be reviewed per implementation gate. PO must separately authorize software changes, deployment and merge.

### 13. Open operational risks, verification and ownership

Design findings R01–R09 are **addressed in specification only**, not resolved in running software. Remaining evidence gaps include bootstrap external provisioning trust and full-restore rollback, privileged MFA before internet production, tested password hash configuration, dependable audit logging, connector validation side effects and full source ACL checks (S02).

**Definition of design acceptance:** This baseline was PO-approved on 2026-10-30. **Definition of remediation completion:** code + negative/positive tests + migration/rollback evidence + independent review + PO acceptance, not the design approval alone.

**Next controlled gates:** (1) Documentation PR review/merge by PO; (2) Implementation planning/authorization; (3) per-package code/review/test/PO merge; (4) S02 and broader enterprise Security Gate.
