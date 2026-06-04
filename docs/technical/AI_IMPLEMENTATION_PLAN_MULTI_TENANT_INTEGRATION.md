# AI Implementation Plan - Multi-Tenant Integration Platform

Date: 2026-06-04

This document is the executable implementation plan for AI-assisted development work. It is based on the current codebase, existing technical documents, and the two integration planning documents:

- `docs/technical/TONG_HOP_LAM_RO_DINH_HUONG_TICH_HOP.md`
- `docs/technical/GOI_Y_DIEU_CHINH_KIEN_TRUC_TICH_HOP.md`

The goal is to evolve Internal Knowledge Copilot from a standalone internal knowledge MVP into a shared multi-tenant platform that can integrate with CRM, sales software, and later third-party business systems.

## 1. How AI Agents Should Use This File

Before starting any implementation batch:

- Read this file first.
- Read the current source files before editing them.
- Pick the smallest coherent checklist batch.
- Mark the selected item as in progress by adding a short note in the progress log.
- After finishing, change `[ ]` to `[x]` only for verified work.
- Add a short dated entry to the progress log with tests run and known gaps.
- Do not mark a phase complete until its acceptance criteria pass.

Recommended progress markers:

```text
[ ] Not started
[x] Done and verified
```

For blocked or partial work, keep the checkbox unchecked and add a note in the progress log.

## 2. Locked Product Decisions

- Deployment model: one shared multi-tenant system for many customers.
- Initial integrations: internal company-owned systems where source code can be changed.
- Future integrations: third-party/customer-owned systems through connector boundaries.
- Knowledge and permission model: hybrid sync. Copilot syncs metadata, content, chunks, embeddings, and ACL snapshots, then revalidates permissions/actions with the source system when needed.
- AI action model: AI may create tasks, update deals, change statuses, or send notifications only after user approval or rule approval.
- CRM won/lost phase 1: reasoning based on process documents, deal stage, recent activities, notes, tasks, emails, and call logs. Do not implement predictive ML until clean historical deal data exists.
- Enterprise requirements: on-premise, data residency, local models, and per-tenant provider isolation are future research items, but the design must not block them.

## 3. Current Codebase Baseline

Backend:

- ASP.NET Core API in `src/backend/InternalKnowledgeCopilot.Api`.
- Existing modules: `Admin`, `Ai`, `AiSettings`, `AuditLogs`, `Auth`, `Dashboard`, `Documents`, `Evaluation`, `Feedback`, `Folders`, `KnowledgeIndex`, `Teams`, `Users`, `Wiki`.
- Existing infrastructure boundaries: AI provider, document processing, file storage, vector store, keyword search, audit, background jobs, knowledge index.
- EF Core with SQLite in `Infrastructure/Database`.
- ChromaDB vector store behind `IKnowledgeVectorStore`.
- Background processing currently runs in the API process through `ProcessingJobWorker`.
- Tests are in `src/backend/InternalKnowledgeCopilot.Tests`.

Frontend:

- Vue app in `src/frontend`.
- Existing pages for auth, admin, AI Q&A, documents, wiki, dashboard, review/evaluation.

Important current strengths:

- Permission checks already exist through `FolderPermissionService`.
- RAG answer generation already uses citations and interaction history.
- Document approval, versioning, indexing, wiki publishing, feedback, evaluation, audit, and retrieval explain already exist.
- AI provider and vector store already have adapter boundaries.

Important gaps for this plan:

- No tenant boundary yet.
- No application/integration boundary yet.
- No external knowledge source model.
- No external ACL snapshot or permission revalidation connector.
- No domain event model for CRM/sales events.
- No workflow recommendation model.
- No AI action approval/execution model.
- Background jobs are not yet separated from the API process.

## 4. Target Architecture

Keep a modular monolith first. Do not split into microservices until the tenant and integration boundaries are stable.

```text
[CRM / Sales / Internal Apps / Future Third-Party Apps]
   |
   | webhooks / sync APIs / embedded widgets
   v
[Internal Knowledge Copilot API]
   |
   +--> Tenant & Application Core
   +--> Knowledge Core
   +--> Integration Layer
   +--> Workflow Copilot
   +--> Action Approval & Execution
   +--> Audit / Feedback / Evaluation
   |
   +--> Relational DB
   +--> File or Object Storage
   +--> Vector Store
   +--> Background Jobs / Worker
   +--> AI Provider Gateway
```

New backend module targets:

```text
src/backend/InternalKnowledgeCopilot.Api/Modules/Tenants
src/backend/InternalKnowledgeCopilot.Api/Modules/Applications
src/backend/InternalKnowledgeCopilot.Api/Modules/KnowledgeSources
src/backend/InternalKnowledgeCopilot.Api/Modules/Integrations
src/backend/InternalKnowledgeCopilot.Api/Modules/WorkflowCopilot
src/backend/InternalKnowledgeCopilot.Api/Modules/ActionApprovals
```

New infrastructure boundary targets:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/AccessControl
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Connectors
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/IntegrationEvents
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/ActionExecution
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Prompting
```

## 5. Global Engineering Rules

- Tenant isolation is mandatory for every user-facing, retrieval, indexing, audit, feedback, and AI action workflow.
- The source application remains the source of truth for real permissions and real business actions.
- Vector metadata may help retrieval filtering, but it must never be the only authorization layer.
- AI recommendations and AI actions must be auditable.
- AI actions must be idempotent.
- External connectors must be isolated behind interfaces; do not hard-code CRM logic inside RAG or document processing services.
- Keep .NET as the main implementation stack. Do not add Python unless a later OCR/layout/model pipeline requires it.
- Keep existing tests passing after every batch.
- Add focused tests for cross-tenant isolation and permission boundaries.

## 6. Verification Commands

Run the relevant commands after each implementation batch.

Backend:

```powershell
dotnet test src/backend/InternalKnowledgeCopilot.sln
```

Frontend:

```powershell
Set-Location src/frontend
npm test
npm run build
```

Smoke scripts, when applicable:

```powershell
scripts/smoke-mvp.ps1
scripts/smoke-milestone8.ps1
```

If a command cannot be run, record the reason in the progress log.

## 7. Phase Progress Summary

- [x] Phase 0 - Preflight and baseline verification
- [x] Phase 1 - Tenant and application foundation
- [x] Phase 2 - Tenantize existing data and query flows
- [x] Phase 3 - Knowledge sources, external objects, and ACL snapshots
- [x] Phase 4 - Integration contracts and connector boundaries
- [x] Phase 5 - Tenant-aware retrieval, indexing, and permission revalidation
- [x] Phase 6 - Workflow Copilot for CRM events
- [ ] Phase 7 - AI action approval and execution
- [ ] Phase 8 - Frontend surfaces and embedded usage
- [ ] Phase 9 - Worker and job hardening
- [ ] Phase 10 - Product hardening, evaluation, and enterprise notes

## 8. Phase 0 - Preflight and Baseline Verification

Goal: confirm the current repo state and avoid mixing unrelated changes with the platform work.

Checklist:

- [x] Run `git status --short` and record existing uncommitted files.
- [x] Run backend tests with `dotnet test src/backend/InternalKnowledgeCopilot.sln`.
- [x] Run frontend tests/build from `src/frontend`.
- [x] Review current EF migrations and confirm the latest model snapshot.
- [x] Review `Program.cs` service registration before adding new services.
- [x] Review existing auth claims and JWT payload in `JwtTokenService`.
- [x] Review existing permission flow in `FolderPermissionService`.
- [x] Review existing retrieval flow in `AiQuestionService`.
- [x] Review existing indexing flow in `DocumentProcessingService`, `KnowledgeChunkLedgerService`, and `KnowledgeKeywordIndexService`.

Acceptance criteria:

- Baseline test status is known.
- Existing dirty files are documented.
- The first implementation batch is selected.

Phase 0 review notes (2026-06-04):

- Dirty files before and after verification: only `?? docs/technical/AI_IMPLEMENTATION_PLAN_MULTI_TENANT_INTEGRATION.md`.
- Backend baseline: `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed, 55/55 tests.
- Frontend baseline: `npm test` passed, 1/1 test; `npm run build` passed.
- EF migrations reviewed: latest migration files by name are `20260513170000_AddAiProviderSettings.cs` and `20260513174500_AddSeparateEmbeddingProviderSettings.cs`; `AppDbContextModelSnapshot.cs` uses EF Core `8.0.11` and includes `AiProviderSettingEntity` embedding provider fields.
- `Program.cs` reviewed: services are registered directly in the API host; tenant services should be added near existing infrastructure/module registrations before middleware is inserted.
- `JwtTokenService` reviewed: current JWT contains user id, email, display name, and role claims; no tenant/application claims exist yet.
- `FolderPermissionService` reviewed: permissions are currently user/team/folder based, with Admin/Reviewer seeing all non-deleted folders.
- `AiQuestionService` reviewed: retrieval validates requested folder/document scope, builds folder/company filters, merges vector and keyword candidates, then revalidates candidates against SQL state.
- Indexing flow reviewed: `DocumentProcessingService` writes vector metadata and updates both `KnowledgeChunkLedgerService` and `KnowledgeKeywordIndexService`; no tenant/application metadata exists yet.
- Selected first implementation batch: Phase 1 / Slice 1 - Foundation Only.

## 9. Phase 1 - Tenant and Application Foundation

Goal: add the base multi-tenant domain without rewriting all existing modules at once.

Recommended new backend files:

```text
Modules/Tenants/TenantModels.cs
Modules/Tenants/TenantsController.cs
Modules/Tenants/TenantService.cs
Modules/Applications/ApplicationModels.cs
Modules/Applications/ApplicationsController.cs
Modules/Applications/ApplicationService.cs
Infrastructure/Database/Entities/TenantEntity.cs
Infrastructure/Database/Entities/ApplicationEntity.cs
Infrastructure/Tenancy/ITenantContext.cs
Infrastructure/Tenancy/TenantContext.cs
Infrastructure/Tenancy/TenantResolutionMiddleware.cs
```

Checklist:

- [x] Add `TenantEntity` with `Id`, `Name`, `Code`, `Status`, `CreatedAt`, `UpdatedAt`, `DeletedAt`.
- [x] Add `ApplicationEntity` with `Id`, `TenantId`, `Code`, `Name`, `ApplicationType`, `BaseUrl`, `Status`, `CreatedAt`, `UpdatedAt`, `DeletedAt`.
- [x] Add tenant/application enums such as `TenantStatus`, `ApplicationStatus`, and `ApplicationType`.
- [x] Add `DbSet<TenantEntity>` and `DbSet<ApplicationEntity>` to `AppDbContext`.
- [x] Configure EF table names, indexes, max lengths, and relationships.
- [x] Create EF migration for tenants and applications.
- [x] Seed a default tenant and default internal application for development.
- [x] Add `ITenantContext` and a simple tenant resolver.
- [x] Support tenant resolution from a safe initial mechanism such as `X-Tenant-Code` for internal APIs.
- [x] Add tenant/application services with validation and soft-delete rules.
- [x] Add admin-only tenant/application controllers.
- [x] Register services in `Program.cs`.
- [x] Add backend tests for create/list/update tenant and application flows.

Acceptance criteria:

- Existing app still runs with a default tenant.
- Tenants and applications can be managed by Admin.
- No existing feature requires a tenant from the user yet.
- Tests cover duplicate tenant/application code handling.

Phase 1 review notes (2026-06-04):

- Added tenant/application foundation without tenantizing existing data tables.
- Added `X-Tenant-Code` resolver with fallback to the seeded `default` tenant; missing default tenant does not block existing API requests.
- Added default development data: tenant `default` and application `internal-knowledge-copilot`.
- Added Admin-only APIs under `api/admin/tenants` and `api/admin/applications`.
- Added service tests for tenant create/list/update/duplicate-code flows and application create/list/update/delete/duplicate-code flows.
- Verification passed: `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 64/64; `npm test` passed 1/1; `npm run build` passed; `dotnet ef migrations list` shows `20260604035437_AddTenantsAndApplications` as the latest migration.

## 10. Phase 2 - Tenantize Existing Data and Query Flows

Goal: make existing MVP data tenant-aware and prevent cross-tenant reads.

High-priority tables to tenantize:

```text
users
teams
folders
folder_permissions
user_folder_permissions
documents
document_versions
processing_jobs
ai_interactions
ai_interaction_sources
ai_feedback
ai_quality_issues
knowledge_corrections
retrieval_hints
knowledge_chunks
knowledge_chunk_indexes
evaluation_cases
evaluation_runs
evaluation_run_results
wiki_drafts
wiki_pages
audit_logs
ai_provider_settings
```

Checklist:

- [x] Add `TenantId` to the entity classes above.
- [x] Create a migration that backfills existing rows into the default tenant.
- [x] Update unique indexes to include `TenantId` where needed, especially email, team name, folder path, and provider settings.
- [x] Update `DevelopmentSeeder` to create tenant-scoped demo users, teams, folders, documents, and settings.
- [x] Update login flow to resolve tenant before authenticating.
- [x] Add tenant claims to JWT.
- [x] Add tenant-aware helpers for controllers and services.
- [x] Update `UsersController` and `UsersService` logic to filter by tenant.
- [x] Update `TeamsController` logic to filter by tenant.
- [x] Update `FoldersController` and `FolderPermissionService` to filter by tenant.
- [x] Update `DocumentsController` and document queries to filter by tenant.
- [x] Update `WikiService` and wiki queries to filter by tenant.
- [x] Update `AiQuestionService` to store and filter interactions by tenant.
- [x] Update `FeedbackController` and `AiFeedbackService` to filter by tenant.
- [x] Update `EvaluationService` to isolate cases/runs/results by tenant.
- [x] Update `AuditLogService` to store tenant id.
- [x] Update `DataResetService` to avoid cross-tenant resets unless explicitly requested by internal admin.
- [x] Add cross-tenant tests for users, folders, documents, wiki, AI Q&A, feedback, evaluation, and audit.

Acceptance criteria:

- A user from tenant A cannot list, retrieve, cite, or act on tenant B data.
- Existing single-tenant usage still works through the default tenant.
- All existing tests pass or are updated intentionally for tenant-aware behavior.
- New cross-tenant isolation tests pass.

Phase 2 review notes (2026-06-04):

- Tenantized existing MVP entities and indexes, including users, teams, folders, documents, wiki, AI interactions, feedback, evaluation, audit logs, knowledge index tables, processing jobs, and AI provider settings.
- Added migration `20260604042515_TenantizeExistingData`; it ensures a default tenant exists, backfills existing rows to the tenant with `code='default'`, and recreates tenant-scoped indexes.
- Login now resolves tenant before authenticating and JWTs include `tenant_id` and `tenant_code` claims.
- Request services/controllers now use `ITenantContext` for tenant-scoped reads and writes; background processing sets tenant context from each processing job before indexing.
- Retrieval, keyword search, ledger rebuild, wiki indexing, correction indexing, and data reset now carry or filter `tenant_id`.
- Added focused cross-tenant isolation tests for read models, AI citation filtering, feedback submission guards, and tenant header/JWT mismatch handling.
- Verification passed: `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 68/68.

## 11. Phase 3 - Knowledge Sources, External Objects, and ACL Snapshots

Goal: represent internal uploads and external business-system documents through a shared knowledge source model.

Recommended new files:

```text
Modules/KnowledgeSources/KnowledgeSourceModels.cs
Modules/KnowledgeSources/KnowledgeSourcesController.cs
Modules/KnowledgeSources/KnowledgeSourceService.cs
Infrastructure/Database/Entities/KnowledgeSourceEntity.cs
Infrastructure/Database/Entities/ExternalObjectEntity.cs
Infrastructure/Database/Entities/ExternalAclSnapshotEntity.cs
```

Checklist:

- [x] Add `KnowledgeSourceEntity` with tenant, application, source type, external source id, name, sync mode, status, and last sync metadata.
- [x] Add `ExternalObjectEntity` with tenant, application, object type, external object id, title, URL, metadata JSON, content hash, ACL hash, and sync timestamps.
- [x] Add `ExternalAclSnapshotEntity` with tenant, application, object identity, subject identity, permission, validity window, and sync timestamp.
- [x] Add EF configuration and migrations.
- [x] Add service methods for upserting knowledge sources.
- [x] Add service methods for upserting external objects by natural key: tenant + application + object type + external object id.
- [x] Add service methods for replacing ACL snapshots safely.
- [x] Add read APIs for Admin/Reviewer to inspect source sync status.
- [x] Map existing local documents/wiki to a default local knowledge source.
- [x] Add tests for idempotent upsert and ACL replacement.

Acceptance criteria:

- Local uploaded documents can coexist with external synced documents.
- External objects can be synced repeatedly without duplicate rows.
- ACL snapshots are tenant-scoped and application-scoped.

Implementation notes:

- Added shared knowledge-source registry, external-object records, and ACL snapshot storage under tenant/application scope.
- Added Admin/Reviewer read APIs plus Admin upsert/replace APIs for source sync inspection and connector writes.
- Local uploads and wiki publishing now attach to the default local knowledge source, with migration backfill for existing local records.
- Verification passed: `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 72/72; `dotnet ef database update` applied all migrations through `20260604085007_AddKnowledgeSourcesAndExternalObjects` on a fresh design-time SQLite database.

## 12. Phase 4 - Integration Contracts and Connector Boundaries

Goal: allow internal CRM/sales systems to send events, documents, permissions, and object context to Copilot through stable contracts.

Recommended new files:

```text
Modules/Integrations/IntegrationModels.cs
Modules/Integrations/IntegrationsController.cs
Modules/Integrations/IntegrationService.cs
Infrastructure/Connectors/IExternalContentClient.cs
Infrastructure/Connectors/IExternalObjectContextClient.cs
Infrastructure/AccessControl/IExternalAccessResolver.cs
Infrastructure/ActionExecution/IExternalActionExecutor.cs
Infrastructure/IntegrationEvents/IntegrationEventValidator.cs
Infrastructure/Database/Entities/IntegrationConnectionEntity.cs
```

Inbound Copilot API targets:

```text
POST /api/integrations/{applicationCode}/events
POST /api/integrations/{applicationCode}/documents/changed
POST /api/integrations/{applicationCode}/objects/sync
POST /api/integrations/{applicationCode}/permissions/sync
```

Expected source-system API targets:

```text
GET  /copilot/documents/{externalId}/content
POST /copilot/permissions/check
POST /copilot/actions/validate
POST /copilot/actions/execute
GET  /copilot/objects/{type}/{externalId}/context
```

Checklist:

- [x] Add `IntegrationConnectionEntity` with tenant, application, base URL, auth mode, status, and secret reference fields.
- [x] Add inbound request models for domain events, document changed events, object sync, and permission sync.
- [x] Add validation for tenant/application resolution.
- [x] Add an initial internal integration authentication mechanism.
- [x] Add idempotency keys for inbound events.
- [x] Store inbound domain events in a durable table in Phase 6 or a temporary integration event table.
- [x] Add connector interfaces without implementing third-party connectors yet.
- [x] Add an internal HTTP connector implementation for company-owned systems.
- [x] Add tests for invalid app code, invalid tenant, duplicate event idempotency, and unauthorized integration calls.

Acceptance criteria:

- Internal apps can push document/object/permission changes.
- Duplicate integration messages do not create duplicate records.
- Connector interfaces are independent of CRM-specific domain logic.

Implementation notes:

- Added tenant/application-scoped integration connections, internal API-key authentication, durable inbound event storage, and idempotency keys across inbound sync/event endpoints.
- Added connector boundaries for external content, object context, permission revalidation, action validation, and action execution, plus an internal HTTP connector implementation for company-owned systems.
- Verification passed: `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 77/77; `dotnet ef database update` applied all migrations through `20260604091137_AddIntegrationContracts` on a fresh design-time SQLite database.

## 13. Phase 5 - Tenant-Aware Retrieval, Indexing, and Permission Revalidation

Goal: extend RAG so external knowledge and tenant/application filters are first-class, while preserving existing citations and safety.

Files likely to change:

```text
Infrastructure/VectorStore/KnowledgeQueryFilter.cs
Infrastructure/VectorStore/KnowledgeChunkRecord.cs
Infrastructure/VectorStore/ChromaKnowledgeVectorStore.cs
Infrastructure/KnowledgeIndex/KnowledgeChunkLedgerService.cs
Infrastructure/KeywordSearch/KnowledgeKeywordIndexService.cs
Infrastructure/DocumentProcessing/DocumentProcessingService.cs
Modules/Ai/AiQuestionService.cs
Modules/KnowledgeIndex/KnowledgeIndexRebuildService.cs
```

Checklist:

- [x] Extend `KnowledgeQueryFilter` with tenant id, application id, knowledge source id, external object type, and external object id.
- [x] Extend vector chunk metadata with tenant/application/source fields.
- [x] Extend `KnowledgeChunkEntity` and `KnowledgeChunkIndexEntity` with tenant/application/source metadata.
- [x] Update document ingestion to write tenant/application/source metadata.
- [x] Update wiki indexing to write tenant/application/source metadata.
- [x] Update Chroma upsert/query metadata mapping.
- [x] Update keyword index filtering by tenant/application/source.
- [x] Update `AiQuestionService` retrieval flow to always include tenant filter.
- [x] Add candidate permission revalidation hook through `IExternalAccessResolver`.
- [x] Revalidate high-risk citations before exposing excerpts from external systems.
- [x] Preserve local `FolderPermissionService` checks for local documents/wiki.
- [x] Add tests proving tenant A cannot retrieve tenant B chunks from vector or keyword search.
- [x] Add tests proving stale ACL snapshots can be rejected by realtime revalidation.

Acceptance criteria:

- AI Q&A only uses chunks within the user's tenant and allowed application scope.
- External-source citations can be revalidated before response generation or before final response persistence.
- Existing local folder/document scoped questions still work.

Implementation notes:

- Added application, knowledge source, and external-object metadata to chunk ledger, keyword index, vector metadata, and AI interaction sources.
- Extended Chroma and keyword filtering with tenant/application/source/object constraints while preserving local folder/company visibility behavior.
- Added external-object retrieval support with ACL snapshot checks and realtime `IExternalAccessResolver` revalidation before external excerpts can reach answer generation.
- Verification passed: `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 80/80; `dotnet ef database update` applied all migrations through `20260604093517_AddRetrievalSourceMetadata` on a fresh design-time SQLite database.

## 14. Phase 6 - Workflow Copilot for CRM Events

Goal: react to business events such as deal stage changes and generate next-step recommendations.

Recommended new files:

```text
Modules/WorkflowCopilot/WorkflowCopilotModels.cs
Modules/WorkflowCopilot/WorkflowCopilotController.cs
Modules/WorkflowCopilot/WorkflowCopilotService.cs
Infrastructure/Database/Entities/WorkflowDefinitionEntity.cs
Infrastructure/Database/Entities/WorkflowStepEntity.cs
Infrastructure/Database/Entities/DomainEventEntity.cs
Infrastructure/Database/Entities/AiRecommendationEntity.cs
Infrastructure/AiProvider/WorkflowRecommendationGenerationService.cs
```

Checklist:

- [x] Add `WorkflowDefinitionEntity`.
- [x] Add `WorkflowStepEntity`.
- [x] Add `DomainEventEntity`.
- [x] Add `AiRecommendationEntity`.
- [x] Add EF configuration and migrations.
- [x] Add API to receive or trigger CRM deal-stage events.
- [x] Add API to list recommendations by tenant/application/object.
- [x] Add service to resolve the workflow definition for an event.
- [x] Add service to fetch or accept object context: deal, stage, notes, tasks, emails, calls, recent activities.
- [x] Add retrieval step for process documents related to the workflow/stage.
- [x] Add AI generation service for workflow recommendations.
- [x] Include next steps, risks, clarification questions, suggested tasks, warnings, and reasoning-based won/lost signals.
- [x] Store recommendation sources/citations.
- [x] Add feedback endpoint for recommendation quality.
- [x] Add tests for deal-stage event to recommendation creation.
- [x] Add tests for missing workflow, missing context, and permission failure.

Acceptance criteria:

- A CRM event can create a stored recommendation.
- Recommendation output is grounded in process documents and event context.
- Won/lost output is labeled as reasoning-based, not predictive ML.
- Recommendation history is visible and auditable.

## 15. Phase 7 - AI Action Approval and Execution

Goal: allow AI to propose business actions and execute them only after user approval or rule approval.

Recommended new files:

```text
Modules/ActionApprovals/ActionApprovalModels.cs
Modules/ActionApprovals/ActionApprovalsController.cs
Modules/ActionApprovals/ActionApprovalService.cs
Infrastructure/Database/Entities/AiActionRequestEntity.cs
Infrastructure/ActionExecution/ActionExecutionModels.cs
Infrastructure/ActionExecution/ExternalActionExecutor.cs
```

Checklist:

- [ ] Add `AiActionRequestEntity` with tenant, application, recommendation, action type, target object, payload, approval mode, status, approver, execution result, idempotency key, and timestamps.
- [ ] Add action statuses: `Draft`, `PendingApproval`, `Approved`, `Rejected`, `Executing`, `Succeeded`, `Failed`, `Cancelled`.
- [ ] Add action request creation from workflow recommendations.
- [ ] Add user approval endpoint.
- [ ] Add user rejection endpoint with reason.
- [ ] Add rule approval abstraction, but keep initial rules simple.
- [ ] Add source-system action validation call before approval or execution.
- [ ] Add source-system action execution through `IExternalActionExecutor`.
- [ ] Enforce idempotency for action execution.
- [ ] Add audit logs for create, approve, reject, execute, fail, and cancel.
- [ ] Add tests for approval lifecycle.
- [ ] Add tests for duplicate execution prevention.
- [ ] Add tests for source-system validation failure.

Acceptance criteria:

- AI cannot directly mutate CRM/sales data without an action request.
- Every action has approval status, audit trail, and idempotency key.
- Source system performs final validation and execution.
- Failed execution is visible and retry-safe.

## 16. Phase 8 - Frontend Surfaces and Embedded Usage

Goal: expose the new platform features to Admin, Reviewer, and business users without disrupting the existing MVP UI.

Recommended frontend targets:

```text
src/frontend/src/api/tenants.ts
src/frontend/src/api/applications.ts
src/frontend/src/api/knowledgeSources.ts
src/frontend/src/api/integrations.ts
src/frontend/src/api/workflowCopilot.ts
src/frontend/src/api/actionApprovals.ts
src/frontend/src/pages/admin/TenantManagementPage.vue
src/frontend/src/pages/admin/ApplicationManagementPage.vue
src/frontend/src/pages/admin/IntegrationManagementPage.vue
src/frontend/src/pages/review/KnowledgeSourcePage.vue
src/frontend/src/pages/workflow/RecommendationListPage.vue
src/frontend/src/pages/workflow/ActionApprovalQueuePage.vue
```

Checklist:

- [ ] Add API clients for tenants and applications.
- [ ] Add Admin pages for tenant/application management.
- [ ] Add API clients for integrations and knowledge sources.
- [ ] Add Admin/Reviewer pages for source sync status.
- [ ] Add workflow recommendation list page.
- [ ] Add recommendation detail page with citations/context.
- [ ] Add action approval queue.
- [ ] Add approve/reject/execute interactions with clear loading/error states.
- [ ] Add tenant/application context handling in API client headers.
- [ ] Add route guards for Admin/Reviewer/User access.
- [ ] Add frontend tests for key pages.
- [ ] Add build verification.

Acceptance criteria:

- Admin can manage tenants/applications/integration settings.
- Reviewer/Admin can inspect knowledge source sync status.
- Business user can view AI recommendations and approve or reject proposed actions.
- UI remains compatible with existing login and MVP pages.

## 17. Phase 9 - Worker and Job Hardening

Goal: make long-running sync, ingestion, recommendation, and action execution jobs safer for production.

Checklist:

- [ ] Add `TenantId`, `ApplicationId`, `IdempotencyKey`, `ScheduledAt`, and richer error metadata to processing jobs.
- [ ] Add job types for document sync, permission sync, object sync, workflow recommendation, action execution, and index rebuild.
- [ ] Ensure all job handlers set tenant/application context.
- [ ] Add retry rules per job type.
- [ ] Add dead-letter or final failed state behavior.
- [ ] Evaluate whether to keep DB-backed polling temporarily or introduce Hangfire/queue.
- [ ] Prepare a separate `.NET Worker` project when job volume grows.
- [ ] Add tests for job claiming, retry, failure, and tenant isolation.

Acceptance criteria:

- Background jobs cannot process the wrong tenant/application.
- Repeated sync/action jobs are idempotent.
- Failed jobs are diagnosable from DB and logs.

## 18. Phase 10 - Product Hardening, Evaluation, and Enterprise Notes

Goal: prepare the platform for real multi-customer operation while keeping enterprise-only items clearly separated.

Checklist:

- [ ] Add prompt template/version storage for major AI tasks.
- [ ] Add `IAiTaskRouter` for task-based model selection.
- [ ] Store provider/model/prompt/retrieval pipeline metadata on AI interactions and recommendations.
- [ ] Add tenant-scoped evaluation cases and evaluation runs.
- [ ] Add evaluation cases for cross-tenant leakage prevention.
- [ ] Add structured logs for integration events, retrieval, recommendations, and actions.
- [ ] Add metrics for sync lag, indexing failures, recommendation latency, approval rate, execution success rate, and incorrect feedback.
- [ ] Add secret handling plan for integration credentials and AI provider keys.
- [ ] Add SQL Server or PostgreSQL migration plan.
- [ ] Add backup/restore and retention notes for tenant data.
- [ ] Add future research notes for on-premise, data residency, local models, and per-tenant provider isolation.

Acceptance criteria:

- AI changes can be evaluated before release.
- Operations can detect broken sync, bad retrieval, and failed actions.
- Enterprise requirements are documented but not mixed into the immediate implementation scope.

## 19. Recommended Implementation Slices

Use these slices for AI-assisted construction. Each slice should end with tests and a progress log update.

### Slice 1 - Foundation Only

- [x] Add tenant/application entities, services, controllers, migration, and seed data.
- [x] Add tests.
- [x] Do not tenantize all existing tables yet.

### Slice 2 - Tenantize Auth and Admin Data

- [ ] Add tenant to users, teams, folders, permissions, and audit.
- [ ] Update login/JWT.
- [ ] Add cross-tenant admin tests.

### Slice 3 - Tenantize Knowledge Core

- [ ] Add tenant to documents, versions, wiki, chunks, feedback, AI interactions, and evaluation.
- [ ] Update services and tests.
- [ ] Prove no cross-tenant retrieval.

### Slice 4 - Knowledge Source Model

- [x] Add knowledge sources, external objects, and ACL snapshots.
- [x] Map local documents to default local source.
- [x] Add sync-status APIs.

### Slice 5 - Integration API

- [x] Add integration connection and inbound sync/event endpoints.
- [x] Add connector interfaces.
- [x] Add idempotency tests.

### Slice 6 - External-Aware Retrieval

- [x] Extend vector/keyword metadata.
- [x] Add source/application filters.
- [x] Add permission revalidation hook.

### Slice 7 - CRM Workflow Recommendation

- [x] Add workflow/domain event/recommendation data model.
- [x] Add event-to-recommendation service.
- [x] Add reasoning-based won/lost recommendation output.

### Slice 8 - Action Approval

- [ ] Add action request model and lifecycle.
- [ ] Add approval/execution APIs.
- [ ] Add idempotent source-system execution boundary.

### Slice 9 - Frontend and Demo Flow

- [ ] Add admin settings pages.
- [ ] Add recommendation and action approval pages.
- [ ] Create a demo CRM event flow.

### Slice 10 - Hardening

- [ ] Add worker/job hardening.
- [ ] Add prompt/model metadata.
- [ ] Add evaluation and observability improvements.

## 20. Minimum Demo Target

The first useful CRM integration demo should include:

- [ ] One default tenant.
- [ ] One internal CRM application.
- [ ] One synced process document or workflow wiki page.
- [ ] One mocked or real CRM deal-stage-changed event.
- [ ] One deal context payload containing stage, notes, tasks, and recent activities.
- [ ] One AI recommendation with next steps, risks, missing information, and reasoning-based won/lost signal.
- [ ] One proposed action such as creating a follow-up task.
- [ ] User approval for the action.
- [ ] Mocked or real execution back to CRM.
- [ ] Audit log and recommendation feedback.

## 21. Non-Goals for the First Implementation Pass

- [ ] Do not implement third-party marketplace connectors.
- [ ] Do not implement predictive ML for won/lost scoring.
- [ ] Do not implement full on-premise packaging.
- [ ] Do not implement full data residency controls.
- [ ] Do not split the backend into microservices.
- [ ] Do not replace ChromaDB unless retrieval scale forces it.
- [ ] Do not add Python services.

## 22. Risk Checklist

Review these risks before each major phase:

- [ ] Cross-tenant data leakage through normal SQL queries.
- [ ] Cross-tenant data leakage through vector search.
- [ ] Cross-tenant data leakage through keyword index.
- [ ] Stale ACL snapshot after source-system permission changes.
- [ ] AI action executed twice because idempotency is missing.
- [ ] AI action executed without source-system final validation.
- [ ] Prompt logs storing sensitive tenant data without policy.
- [ ] Integration secret stored without an upgrade path to encryption.
- [ ] Long-running jobs blocked because they run inside the API process.
- [ ] Tests passing with mock AI but failing with real provider configuration.

## 23. Progress Log

Add entries here after each implementation batch.

| Date | Agent | Batch | Completed | Verification | Notes |
| --- | --- | --- | --- | --- | --- |
| 2026-06-04 | Codex | Planning | Created this implementation plan | Not run; documentation-only change | Ready for Phase 0 |
| 2026-06-04 | Codex | Phase 0 - Preflight | Verified repo baseline, tests, EF snapshot, service registration, auth claims, permission flow, retrieval flow, and indexing flow | `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 55/55; `npm test` passed 1/1; `npm run build` passed | Dirty file documented: only this untracked plan file. Next batch selected: Phase 1 / Slice 1 - Foundation Only |
| 2026-06-04 | Codex | Phase 1 - Tenant and application foundation | Added tenant/application domain, EF mapping, migration, default seed data, tenant resolver, admin services/controllers, and focused backend tests | `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 64/64; `npm test` passed 1/1; `npm run build` passed; `dotnet ef migrations list` confirmed `20260604035437_AddTenantsAndApplications` latest | Existing tables are not tenantized yet. Next batch: Phase 2 - Tenantize Existing Data and Query Flows |
| 2026-06-04 | Codex | Phase 2 - Tenantize existing data and query flows | Added tenant ids to existing domain tables, tenant-scoped indexes, default-tenant backfill migration, tenant-aware auth/JWT, scoped query/write paths, tenant-aware indexing/rebuild/reset behavior, and cross-tenant backend tests | `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 68/68 | Existing MVP flows now resolve and enforce tenant context. Next batch: Phase 3 - Knowledge Sources, External Objects, and ACL Snapshots |
| 2026-06-04 | Codex | Phase 3 - Knowledge sources, external objects, and ACL snapshots | Added tenant/application-scoped knowledge sources, external objects, ACL snapshots, Admin/Reviewer inspection APIs, default local source mapping for documents/wiki, and idempotent sync tests | `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 72/72; `dotnet ef database update` applied all migrations through `20260604085007_AddKnowledgeSourcesAndExternalObjects` on a fresh design-time SQLite database | External knowledge can now be represented without duplicate object rows, and ACL replacement is scoped to the target tenant/application/object. Next batch: Phase 4 - Integration Contracts and Connector Boundaries |
| 2026-06-04 | Codex | Phase 4 - Integration contracts and connector boundaries | Added integration connections, internal API-key auth, inbound event/document/object/permission sync contracts, durable idempotent inbound event storage, connector interfaces, internal HTTP connector, and focused backend tests | `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 77/77; `dotnet ef database update` applied all migrations through `20260604091137_AddIntegrationContracts` on a fresh design-time SQLite database | Internal applications can now push sync/events without duplicate rows, and connector boundaries remain independent of CRM-specific logic. Next batch: Phase 5 - Tenant-Aware Retrieval, Indexing, and Permission Revalidation |
| 2026-06-04 | Codex | Phase 5 - Tenant-aware retrieval, indexing, and permission revalidation | Added application/source/external-object metadata to retrieval indexes, extended vector and keyword filters, preserved local folder permission checks, added external ACL snapshot plus realtime revalidation before answer generation, and added retrieval isolation tests | `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 80/80; `dotnet ef database update` applied all migrations through `20260604093517_AddRetrievalSourceMetadata` on a fresh design-time SQLite database | Retrieval is now tenant-scoped, optionally application/source/object-scoped, and external citations can be rejected when source-system revalidation denies access. Next batch: Phase 6 - Workflow Copilot for CRM Events |
| 2026-06-05 | Codex | Phase 6 - Workflow Copilot for CRM Events | Added workflow definitions/steps, durable domain events, AI recommendations with citations and feedback, deal-stage event API, recommendation history API, object-context accept/fetch flow, process-document retrieval, and mock/OpenAI-compatible recommendation generation with reasoning-based won/lost signals | `dotnet test src/backend/InternalKnowledgeCopilot.sln` passed 84/84; `dotnet ef database update` applied all migrations through `20260604175019_AddWorkflowCopilot` on a fresh design-time SQLite database | CRM deal-stage events can now create auditable recommendations grounded in event context and retrieved process sources. Won/lost signals are explicitly labeled reasoning-based, not predictive ML. Next batch: Phase 7 - AI Action Approval and Execution |
