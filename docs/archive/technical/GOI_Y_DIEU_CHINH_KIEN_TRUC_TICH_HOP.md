# Goi y dieu chinh kien truc va cach chia code cho nen tang tich hop

Ngay lap: 2026-06-03

Tai lieu nay da duoc archive. Huong dan hien hanh nam o `docs/technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md`.

Tai lieu nay de xuat huong dieu chinh Internal Knowledge Copilot dua tren cac quyet dinh da lam ro trong `docs/archive/technical/TONG_HOP_LAM_RO_DINH_HUONG_TICH_HOP.md`.

## 1. Dinh huong kien truc

Nen phat trien theo huong modular monolith truoc, chua can tach microservice ngay.

Ly do:

- Code hien tai dang la ASP.NET Core API voi cac module ro nhu Documents, Wiki, AI, Feedback, Evaluation, Folders.
- Nhu cau tiep theo can them tenant, integration, workflow va action approval. Cac phan nay co the chia module trong cung solution truoc.
- Tach microservice qua som se lam tang chi phi van hanh, auth, tracing, deploy va data consistency.

Kien truc muc tieu gan:

```text
[Business Apps: CRM / Sales / Other Internal Apps]
   |
   | Webhook / API / Embedded Widget
   v
[Internal Knowledge Copilot API]
   |
   +--> Tenant & Application Core
   +--> Knowledge Core
   +--> Integration Layer
   +--> Workflow Copilot
   +--> Action Approval & Execution
   +--> Audit / Evaluation / Feedback
   |
   +--> Relational DB
   +--> File/Object Storage
   +--> Vector Store
   +--> Background Worker / Queue
   +--> AI Provider Gateway
```

## 2. Nguyen tac thiet ke chinh

- Tenant boundary la bat buoc trong moi luong nghiep vu.
- He thong goc la source of truth cho quyen that va action that.
- Copilot duoc phep sync index va ACL snapshot de retrieval nhanh.
- Truoc khi action hoac hien thi noi dung nhay cam, can revalidate voi he thong goc.
- AI khong tu sua tri thuc/action truc tiep ma di qua policy, approval va audit.
- Connector khong duoc lam ro ri logic cua Knowledge Core.
- Permission phai nam trong relational DB hoac he thong goc, khong dua vao vector metadata de enforce.

## 3. Chia module backend de xuat

Hien tai co the giu `InternalKnowledgeCopilot.Api`, nhung nen them module theo chieu ngang:

```text
src/backend/InternalKnowledgeCopilot.Api/Modules/Tenants
src/backend/InternalKnowledgeCopilot.Api/Modules/Applications
src/backend/InternalKnowledgeCopilot.Api/Modules/KnowledgeSources
src/backend/InternalKnowledgeCopilot.Api/Modules/Integrations
src/backend/InternalKnowledgeCopilot.Api/Modules/WorkflowCopilot
src/backend/InternalKnowledgeCopilot.Api/Modules/ActionApprovals
```

Them infrastructure boundary:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/AccessControl
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Connectors
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/IntegrationEvents
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/ActionExecution
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Prompting
```

Vai tro cua tung module:

| Module | Vai tro |
| --- | --- |
| Tenants | Quan ly tenant, tenant settings, trang thai tenant |
| Applications | Khai bao CRM, Sales, Wiki Portal hoac san pham tich hop |
| KnowledgeSources | Bieu dien nguon tri thuc local/external, version, sync status |
| Integrations | Webhook, sync job, connector config, external object mapping |
| WorkflowCopilot | Xu ly event nghiep vu va sinh goi y theo quy trinh |
| ActionApprovals | Tao, duyet, reject, execute va audit AI action |
| AccessControl | Interface revalidate quyen voi he thong goc va ACL snapshot |
| Connectors | Adapter cho CRM noi bo, Sales noi bo, sau nay ben thu ba |
| ActionExecution | Adapter gui lenh thuc thi ve he thong goc |
| Prompting | Quan ly prompt version theo task va tenant |

## 4. Mo hinh du lieu nen bo sung

### 4.1 Tenant va application

```text
tenants
- id
- name
- code
- status
- created_at
- updated_at

applications
- id
- tenant_id
- code
- name
- application_type
- base_url
- status
- created_at
- updated_at
```

Tat ca cac bang nghiep vu hien co nen duoc nghien cuu them `tenant_id`, dac biet:

- users
- teams
- folders
- documents
- document_versions
- wiki_pages
- ai_interactions
- ai_feedback
- audit_logs
- knowledge_chunks
- knowledge_chunk_indexes
- ai_provider_settings

### 4.2 Knowledge source va external object

```text
knowledge_sources
- id
- tenant_id
- application_id
- source_type
- external_source_id
- name
- sync_mode
- status
- last_synced_at
- created_at
- updated_at

external_objects
- id
- tenant_id
- application_id
- object_type
- external_object_id
- title
- url
- metadata_json
- content_hash
- acl_hash
- last_synced_at
- created_at
- updated_at
```

Dung de gan cac tai lieu/quy trinh/deal/activity tu he thong goc voi Copilot ma khong phu thuoc vao ID noi bo cua tung san pham.

### 4.3 ACL snapshot va revalidation

```text
external_acl_snapshots
- id
- tenant_id
- application_id
- object_type
- external_object_id
- subject_type
- subject_external_id
- permission
- valid_from
- valid_until
- last_synced_at
```

ACL snapshot dung cho filter nhanh. Khi can chac chan, goi connector:

```csharp
public interface IExternalAccessResolver
{
    Task<ExternalAccessDecision> CanAccessAsync(ExternalAccessCheckRequest request, CancellationToken cancellationToken);
}
```

### 4.4 Workflow va AI recommendation

```text
workflow_definitions
- id
- tenant_id
- application_id
- business_object_type
- name
- version
- status
- created_at
- updated_at

workflow_steps
- id
- workflow_definition_id
- step_code
- name
- description
- entry_conditions_json
- recommended_actions_json
- risk_rules_json
- order

domain_events
- id
- tenant_id
- application_id
- event_type
- object_type
- external_object_id
- actor_external_user_id
- payload_json
- occurred_at
- processed_at

ai_recommendations
- id
- tenant_id
- application_id
- domain_event_id
- object_type
- external_object_id
- recommendation_type
- summary
- reasoning
- confidence
- proposed_actions_json
- status
- created_at
- updated_at
```

### 4.5 Action approval va execution

```text
ai_action_requests
- id
- tenant_id
- application_id
- recommendation_id
- action_type
- target_object_type
- target_external_object_id
- payload_json
- approval_mode
- status
- requested_by_user_id
- approved_by_user_id
- approved_at
- executed_at
- execution_result_json
- idempotency_key
- created_at
- updated_at
```

Trang thai goi y:

```text
Draft
PendingApproval
Approved
Rejected
Executing
Succeeded
Failed
Cancelled
```

## 5. API/contract tich hop de xuat

Giai doan dau vi cong ty kiem soat source code CRM/ban hang, nen tao contract noi bo ro rang:

```text
POST /api/integrations/{applicationCode}/events
POST /api/integrations/{applicationCode}/documents/changed
POST /api/integrations/{applicationCode}/objects/sync
POST /api/integrations/{applicationCode}/permissions/sync
POST /api/workflow-copilot/recommendations
POST /api/action-approvals/{id}/approve
POST /api/action-approvals/{id}/reject
POST /api/action-approvals/{id}/execute
```

Phia he thong goc nen cung cap API cho Copilot:

```text
GET  /copilot/documents/{externalId}/content
POST /copilot/permissions/check
POST /copilot/actions/validate
POST /copilot/actions/execute
GET  /copilot/objects/{type}/{externalId}/context
```

Trong do:

- `permissions/check` dung de revalidate quyen.
- `actions/validate` kiem tra action co hop le khong truoc khi duyet/thuc thi.
- `actions/execute` la noi he thong goc thuc hien thay doi that.
- `objects/context` tra ve deal/activity/note/task/email/call log da duoc rut gon theo object.

## 6. Luong CRM de xuat

Vi du khi nguoi dung keo deal sang mot trang thai moi:

```text
1. CRM ghi nhan deal stage changed.
2. CRM gui domain event sang Copilot.
3. Copilot luu domain_event.
4. Copilot lay deal context tu CRM hoac cache context da sync.
5. Copilot xac dinh workflow/stage lien quan.
6. Copilot retrieval tai lieu/quy trinh phu hop trong tenant/application.
7. Copilot sinh recommendation:
   - buoc tiep theo
   - rui ro
   - cau hoi can lam ro
   - viec nen tao
   - canh bao neu thieu activity
   - nhan dinh won/lost dang rule/reasoning based
8. Neu co proposed action, tao ai_action_request.
9. User hoac rule duyet.
10. Copilot goi CRM actions/execute.
11. CRM thuc thi va tra ket qua.
12. Copilot luu audit va feedback.
```

Giai doan dau won/lost nen la:

- Rule + LLM reasoning dua tren quy trinh.
- Co giai thich va citation tu quy trinh/activity.
- Co feedback cua sales/user.
- Chua nen goi la predictive score neu chua co du lieu lich su du sach.

## 7. Dieu chinh retrieval/indexing

Can mo rong `KnowledgeQueryFilter` va metadata chunk theo:

- tenant_id
- application_id
- knowledge_source_id
- external_object_type
- external_object_id
- visibility/acl snapshot hash
- source_version

Vector collection nen co chinh sach ro:

- Giai doan dau: collection dung chung nhung filter bat buoc theo tenant/application.
- Khi du lieu lon hon: collection theo tenant hoac theo tenant + application.
- Khong bao gio dua filter vector la lop bao mat duy nhat.

Pipeline retrieval nen la:

```text
question/event context
-> resolve tenant/application/user
-> load ACL snapshot scope
-> vector + keyword search
-> merge/rerank candidates
-> revalidate candidates can thiet voi he thong goc
-> context packing
-> AI generation
-> citation/action validation
-> persist interaction/recommendation/audit
```

## 8. Dieu chinh AI provider va prompt

Nen them lop route theo task:

```csharp
public interface IAiTaskRouter
{
    Task<AiTaskModelSelection> SelectAsync(AiTaskRequest request, CancellationToken cancellationToken);
}
```

Task nen tach:

- AnswerQuestion
- GenerateWikiDraft
- UnderstandDocument
- GenerateWorkflowRecommendation
- EvaluateDealRisk
- ClassifyFeedback
- ValidateProposedAction

Prompt nen co version:

```text
prompt_templates
- id
- tenant_id nullable
- task_type
- version
- content
- output_schema_json
- status
- created_at
```

Moi AI interaction/recommendation nen luu:

- tenant_id
- application_id
- task_type
- provider/model
- prompt_version
- retrieval_pipeline_version
- context source ids
- latency
- confidence
- output schema version

## 9. Worker va queue

Hien tai `ProcessingJobWorker` nam trong API process. Huong tiep theo nen tach thanh worker rieng khi bat dau tich hop CRM/tai lieu lon:

```text
InternalKnowledgeCopilot.Api
InternalKnowledgeCopilot.Worker
```

Job nen gom:

- DocumentSyncJob
- PermissionSyncJob
- DocumentIngestionJob
- WorkflowRecommendationJob
- ActionExecutionJob
- IndexRebuildJob

Trong giai doan dau co the van dung DB-backed job table, nhung can thiet ke `job_type`, `tenant_id`, `application_id`, retry va idempotency ro rang.

## 10. Lo trinh de xuat

### Giai doan 1. Nen mong multi-tenant va integration boundary

- Them `Tenant` va `Application`.
- Them `tenant_id` cho cac bang quan trong.
- Them global query discipline de tranh query thieu tenant filter.
- Them module `Integrations` va contract event noi bo.
- Them `KnowledgeSources` de dai dien nguon local/external.

### Giai doan 2. Hybrid sync va ACL snapshot

- Xay sync API cho document metadata/content/ACL.
- Them `external_objects` va `external_acl_snapshots`.
- Mo rong indexing theo tenant/application/source.
- Them `IExternalAccessResolver`.
- Revalidate quyen cho citation/action nhay cam.

### Giai doan 3. Workflow Copilot cho CRM

- Them `domain_events`.
- Them `workflow_definitions` va `workflow_steps`.
- Them API nhan event deal stage changed.
- Sinh recommendation dua tren quy trinh, deal context va activity context.
- Luu feedback tren recommendation.

### Giai doan 4. Action approval va execution

- Them `ai_action_requests`.
- Them approval policy.
- Them idempotency key va audit day du.
- Them connector action executor cho CRM noi bo.
- UI cho user duyet/reject/thuc thi action.

### Giai doan 5. Product hardening

- Tach worker rieng.
- Chuyen SQLite sang SQL Server hoac PostgreSQL.
- Them observability, structured logs, metrics va trace.
- Nghien cuu encryption-at-rest cho secret/API key.
- Nghien cuu on-premise, data residency va provider theo tenant.

## 11. Nguyen tac cat scope

Neu can lam nhanh demo tich hop CRM, cat theo thu tu:

1. Chua lam connector ben thu ba.
2. Chua lam predictive ML won/lost.
3. Chua lam on-premise/data residency.
4. Chua lam approval policy phuc tap, chi dung user approval/manual rule don gian.
5. Chua tach microservice, chi tach interface va module trong monolith.

Khong nen cat:

- Tenant boundary.
- Audit cho AI recommendation/action.
- Idempotency khi execute action.
- Revalidate quyen/action voi he thong goc.
- Tach connector boundary khoi AI/Knowledge core.
