# Enterprise AI Copilot — Architecture Review Consolidation (AR-B-01–AR-B-04)

**Version:** 1.0  
**Content status:** APPROVED by Product Owner (2026-10-29)  
**Publication status:** Documentation Publication PR; pending PO review/merge  
**Implementation status:** NOT AUTHORIZED  
**Reference baseline:** `pvanh2706/internal-knowledge-copilot` — `main` @ `15badea17f4e7eddcd8dc71ee0a643fea4e1a47d`  
**Verification method:** Independent read-only static code/document review. No runtime tests were executed in these reviews.

> PO approval confirms review content and architectural direction. It does not mean findings are fixed, production readiness is proven, or development work is authorized.

## 1. Executive Summary and Review Scope

| Review | Subject | Status |
|---|---|---|
| AR-B-01 | Existing Architecture Baseline | APPROVED |
| AR-B-02 | Security & Integration Boundary; SEC-01..07 | APPROVED; findings OPEN |
| AR-B-03 | Technical Debt & Enterprise Readiness; TD-01..06 | APPROVED; findings OPEN |
| AR-B-04 | Consolidated As-Is/To-Be, D1=B D2=B D3=B, preliminary S01..05 | APPROVED; implementation not authorized |

**Approved product constraints:** B is the primary platform; A remains paused; enterprise copilot integrates Jira/CRM/ERP through APIs; source systems own their issues, deals, cases, workflows, permissions and business state ([DEC-0001](../decisions/DEC-0001.md), [DEC-0002](../decisions/DEC-0002.md), [DEC-0003](../decisions/DEC-0003.md)).

## 2. Evidence and Verification Limitations

Baseline technologies: ASP.NET Core/.NET 8 modular monolith, Vue 3/TypeScript/Vite; EF Core + SQLite metadata, local file storage, ChromaDB via vector abstraction, keyword retrieval; hosted background processing; AI providers; tenant/application/connection integration foundation. See [MVP Architecture](../../KIẾN_TRÚC_MVP.md), [Technical Overview](../technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md), [Operations Notes](../technical/ENTERPRISE_OPERATIONS_NOTES.md).

Source files and existing test projects were inspected statically. **Not executed or independently verified**: `dotnet test`, frontend build/test, runtime auth exploits, production secret management, performance/load, failover, backup/restore drill, real Jira/CRM E2E. Historical tests documented in existing phase records are not fresh test evidence for this commit.

## 3. Architecture As-Is

```text
Vue 3 UI
  -> ASP.NET Core modular API
       |-- Auth/Tenant/Folder/Permission/Knowledge/AI/Workflow
       |-- SQLite (source of truth for internal knowledge, audit, jobs, configuration)
       |-- Local File Storage
       |-- ChromaDB via IKnowledgeVectorStore + keyword index
       |-- BackgroundService + processing_jobs
       |-- AI Providers
       `-- Generic HTTP Connector --> External Systems
```

Strengths to retain: modular monolith, abstractions for vector/AI/external connector, document approve/index pipeline, Hybrid RAG with citations, internal folder ACL, audit/feedback. Generic external connector does not equal a production-ready native Jira/Bitrix/CRM adapter.

## 4. Security Findings — AR-B-02 (All PO-accepted, All OPEN)

| ID | Priority | Finding | Code evidence | Required acceptance direction |
|---|---|---|---|---|
| SEC-01 | P0 | Tenant Admin vs platform authority conflated on tenant management endpoints | [TenantsController](../../src/backend/InternalKnowledgeCopilot.Api/Modules/Tenants/TenantsController.cs), [TenantService](../../src/backend/InternalKnowledgeCopilot.Api/Modules/Tenants/TenantService.cs) | Negative cross-tenant management tests; platform-only cross-tenant control |
| SEC-02 | P0 | Approval/execution authorization and auto-rule scope insufficiently restricted | [ActionApprovalService](../../src/backend/InternalKnowledgeCopilot.Api/Modules/ActionApprovals/ActionApprovalService.cs) | Unauthorized approve/execute denied; read-only deployment cannot write to source |
| SEC-03 | P0 | Missing external object/connector paths and workflow retrieval can bypass full revalidation | [WorkflowCopilotService](../../src/backend/InternalKnowledgeCopilot.Api/Modules/WorkflowCopilot/WorkflowCopilotService.cs), [ActionApprovalService](../../src/backend/InternalKnowledgeCopilot.Api/Modules/ActionApprovals/ActionApprovalService.cs) | Deny unknown/missing authority; do not pack inaccessible chunks into context |
| SEC-04 | P1 | Stale ACL snapshot risk; AI Q&A may allow using snapshot without active connector | [AiQuestionService](../../src/backend/InternalKnowledgeCopilot.Api/Modules/Ai/AiQuestionService.cs) | Fail-closed with explicit permission-freshness policy |
| SEC-05 | P1 | Active inbound connections may use `IntegrationAuthMode.None` | [IntegrationService](../../src/backend/InternalKnowledgeCopilot.Api/Modules/Integrations/IntegrationService.cs) | Enterprise ingress authentication/allowlist, negative tests |
| SEC-06 | P1 | Outbound secret reference not resolved to API key at caller paths | [InternalHttpExternalConnector](../../src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Connectors/InternalHttpExternalConnector.cs) | Credential provider and outbound auth integration tests |
| SEC-07 | P1 | BaseUrl URI validation insufficient for SSRF/egress threat | [IntegrationService](../../src/backend/InternalKnowledgeCopilot.Api/Modules/Integrations/IntegrationService.cs) | Scheme/hostname/IP/redirect/DNS controls with tests |

These are static review risks, not claims of a demonstrated live exploit. Source Systems remain permission authority for external data.

## 5. Technical Debt — AR-B-03 (All PO-accepted, All OPEN)

| ID | Priority | Finding | Code evidence | Required acceptance direction |
|---|---|---|---|---|
| TD-01 | P1 | Running jobs have no verified stale lease/crash recovery | [ProcessingJobService](../../src/backend/InternalKnowledgeCopilot.Api/Infrastructure/BackgroundJobs/ProcessingJobService.cs) | Restart can reclaim safely without endless Running status |
| TD-02 | P1 | Pending jobs read/selected in memory then updated; claim is not atomic for multiple workers | [ProcessingJobService](../../src/backend/InternalKnowledgeCopilot.Api/Infrastructure/BackgroundJobs/ProcessingJobService.cs) | Concurrency test prevents double claim/execution |
| TD-03 | P1 | Index/ledger/keyword changes can diverge on partial failure | [DocumentProcessingService](../../src/backend/InternalKnowledgeCopilot.Api/Infrastructure/DocumentProcessing/DocumentProcessingService.cs) | Detect/reconcile partial indexing failures; valid citations after rebuild |
| TD-04 | P1 | Local backup omits consistent full recovery unit/key ring verification | [Backup Script](../../scripts/backup-local.ps1) | DB/file/keys/vector reconstruction restore test |
| TD-05 | P2 | Health reports Healthy even when DB cannot connect, and exposes storage/config paths | [HealthController](../../src/backend/InternalKnowledgeCopilot.Api/Controllers/HealthController.cs) | Dependency-aware readiness and non-sensitive response |
| TD-06 | P2 | IIS migration documentation drift; CI/rollback readiness not independently verified | [IIS Guide](../../TRIỂN_KHAI_IIS.md), [Program](../../src/backend/InternalKnowledgeCopilot.Api/Program.cs) | Docs aligned to runtime, CI build/tests, deployment/rollback evidence |

No immediate replacement of SQLite, worker architecture or vector provider is approved simply from these findings.

## 6. Proposed To-Be Logical Architecture (AR-B-04 Approved)

```text
Vue 3 UI
  -> ASP.NET Core Modular Monolith
      |-- Identity + Tenant / Platform Policy Enforcement
      |-- Knowledge / Hybrid RAG with source revalidation
      |-- Read-only Context Gateway --> Source-specific adapters --> Jira/CRM/ERP
      |-- Reliable Job Processing (claim/retry/lease/recovery)
      |-- SQLite initially; database decision from workload benchmark
      |-- Vector abstraction --> ChromaDB presently
      `-- Audit / Health / Metrics / Backup / Restore controls
```

Logical boundaries do not imply new microservices. Source systems own business records, lifecycle and native authorization; Copilot owns internal knowledge, retrieval/citation and AI assistance. No automatic reuse of paused product A or case/process recreation.

## 7. Approved Architecture Decisions

| Decision | Selected option | Consequence |
|---|---|---|
| [DEC-0004](../decisions/DEC-0004.md) | Strict Security Boundary A | Fail-closed; initial read-only; production Security Gate |
| [DEC-0005](../decisions/DEC-0005.md) | D1=B, separate Platform/Tenant Admin | Role/token compatibility needs separate detailed design |
| [DEC-0006](../decisions/DEC-0006.md) | D2=B, centralized authorization/revalidation | May increase latency; cache/freshness policy needs design |
| [DEC-0007](../decisions/DEC-0007.md) | D3=B, incremental hardening | Benchmark before DB/queue/storage migration |

API contracts, migration schema, deployment topology, security policy implementation and integration adapters are not pre-approved.

## 8. Preliminary Slices S01–S05 (Planning Baseline Only)

| Slice | Findings | Scope | Proposed acceptance criteria |
|---|---|---|---|
| S01 — Identity & Authorization | SEC-01..02 | Separate platform/tenant administration, scope actions; read-only write restrictions | Cross-tenant admin denied, action negative tests, writes disabled by default |
| S02 — External Trust & Secure Retrieval | SEC-03..07 | Shared policy checks, ACL revalidation, connector ingress/outbound authentication, egress controls | Unknown/revoked access denied before prompt/citation, connector auth and SSRF tests |
| S03 — Job Reliability | TD-01..02 | Crash recovery, atomic claiming, idempotency, retry | Worker restart and multi-worker contention tests |
| S04 — Data & Index Recovery | TD-03..04 | Index reconciliation, DB/storage/key/vector restoration | Fault injection and successful restore/retrieval drill |
| S05 — Operational Readiness | TD-05..06 | Health/readiness, CI, observability, deployment/rollback | Health failure behavior, CI checks, documented rollback drill |

**Indicative dependencies:** S01 → S02 Security Gate; S03 and S05 partly parallel once core policy is designed; S04 after reliability foundation. CI/test quality controls should start with S01, not wait until S05. Slice implementation must be separately scoped, designed, authorized and accepted by PO.

## 9. Outstanding Risks and Verification Gates

- All thirteen findings remain OPEN until remediation is reviewed and verified.
- Native Jira/CRM adapters and automated external document content ingestion are separate future workstream designs; inbound `documents/changed` should not be called end-to-end integrated without verification.
- Security Gate required before enterprise production data: tests for cross-tenant boundaries, unauthorized external content/action, ACL refresh failure and connector controls.
- Reliability/production gate requires worker restart/concurrency, indexing fault recovery, backup/restore, metrics, CI and runtime/load validation.
- Hybrid RAG quality on actual data and cost/latency require independent evaluation.
- Architecture review did not execute tests or verify production environment.

## 10. Publication and Next Steps

PO approved AR-B-01..AR-B-04, SEC-01..07, TD-01..06, DEC-0004..07 directions, As-Is/To-Be and S01..05 preliminary plan on 2026-10-29. PO separately authorized **documentation-only branch/commit/push/PR**, but **not merge or code implementation**.

Review this PR and request any edits. If PO authorizes merge, `main` becomes the versioned source of truth for this review. The next technical step is to design S01 acceptance tests and scope; no coding without an independent PO authorization.

**References:** [Project Context](../../PROJECT_CONTEXT.md), [Project Status](../../PROJECT_STATUS.md), [Decision Registry](../decisions/DECISION_LOG.md).
