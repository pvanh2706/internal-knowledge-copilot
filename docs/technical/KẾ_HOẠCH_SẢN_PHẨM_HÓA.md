# Ke hoach nang cap Internal Knowledge Copilot thanh san pham thuc te

Ngay lap: 2026-05-17

Tai lieu nay mo ta ke hoach nang cap Internal Knowledge Copilot tu MVP/pilot noi bo thanh mot san pham co the dung that trong doanh nghiep va co the ban cho khach hang. Muc tieu khong phai them cong nghe cho nhieu, ma la lam he thong on dinh, bao mat, mo rong duoc, do duoc chat luong AI, va co quy trinh van hanh ro rang.

## 1. Ket luan dieu hanh

Nen phat trien theo 3 chang:

1. Product Beta: chay duoc cho nhieu team noi bo voi cau hinh that, SSO, monitoring co ban, test tu dong va runbook van hanh.
2. Product v1: san sang dung that trong cong ty, co database production, worker rieng, backup/restore, security scan, wiki editor/versioning, evaluation gate.
3. Commercial v2: co kha nang ban cho khach hang, ho tro multi-tenant/deployment model, billing/licensing neu can, audit/compliance day du, vector/search production va goi cai dat/onboarding chuan.

Nguyen tac quan trong:

- Giu .NET lam loi he thong. Chua can them Python.
- Chi them Python khi sau nay can self-host OCR/layout/reranker/model pipeline phuc tap.
- SQLite chi phu hop MVP/pilot nho. Ban dung that nen chuyen sang PostgreSQL hoac SQL Server.
- ChromaDB co the giu cho dev/test. Production nen danh gia Qdrant neu du lieu lon va can filter/search on dinh hon.
- AI khong duoc tu sua tri thuc ma khong co reviewer/admin approve.
- Permission source of truth van nam trong relational database, khong nam trong vector DB.

## 2. Hien trang nen tang

He thong hien tai da co cac khoi quan trong:

- Auth local username/password, role Admin/Reviewer/User.
- Team, folder, folder permission.
- Upload, approve/reject, versioning tai lieu.
- File storage ngoai public web root.
- Document processing: extract, normalize, section detection, chunk, embedding, indexing.
- ChromaDB vector store qua adapter.
- SQLite ledger cho knowledge chunks va keyword index.
- AI Q&A co citation, confidence, missing information, conflict metadata.
- Wiki draft va publish.
- Feedback incorrect, quality issue, correction approval.
- Evaluation cases/runs/results.
- Retrieval explain.
- Knowledge index rebuild tu ledger.
- Dashboard va audit log.

Diem can xu ly truoc khi product hoa:

- Cau hinh embedding local/test dang de de fail neu khong co real key. Local/test nen mac dinh `mock`, production bat buoc cau hinh real provider.
- SQLite chua phu hop multi-team/multi-customer production dai han.
- Background jobs dang nam trong API process.
- Chua co SSO.
- Chua co malware scan, PII/secret detection truoc khi index.
- Chua co observability production day du.
- Chua co Playwright E2E cho cac luong chinh.
- Chua co wiki editor/versioning day du.
- Chua co OCR/layout parsing cho scanned PDF/anh trong tai lieu.

## 3. Muc tieu san pham

### 3.1 Muc tieu cho nguoi dung

- Tim cau tra loi noi bo nhanh hon so voi hoi nguoi khac hoac tim file thu cong.
- Cau tra loi co nguon ro rang va chi dua tren tri thuc user co quyen xem.
- Biet khi nao thong tin thieu, cu, mau thuan, hoac can reviewer xac nhan.
- Gui feedback de he thong cai thien that su sau moi loi.

### 3.2 Muc tieu cho reviewer/knowledge owner

- Kiem soat tai lieu nao duoc dung cho AI.
- Chuan hoa tai lieu thanh wiki co cau truc.
- Xu ly feedback sai thanh correction co approve.
- Thay duoc knowledge gap, tai lieu duoc hoi nhieu, tai lieu can cap nhat.

### 3.3 Muc tieu cho admin/IT/security

- SSO, phan quyen, audit, backup, restore, monitoring ro rang.
- Khong leak du lieu trai quyen qua retrieval, prompt, citation, log.
- Co cach cau hinh AI provider va data residency theo chinh sach cong ty.
- Co runbook de van hanh va xu ly su co.

### 3.4 Muc tieu thuong mai

- Co the cai dat cho nhieu khach hang hoac nhieu business unit.
- Co tai lieu onboarding, deployment, security, operation.
- Co goi tinh nang ro: pilot, standard, enterprise.
- Co KPI chung minh gia tri: thoi gian tim thong tin, ty le answer correct, so feedback resolved, wiki published, active users.

## 4. Kien truc muc tieu

```text
[Vue Frontend]
   |
   v
[ASP.NET Core API]
   |
   +--> [PostgreSQL hoac SQL Server]
   |      - users / roles / teams / folders / permissions
   |      - documents / versions / processing metadata
   |      - wiki drafts / wiki pages / wiki versions
   |      - ai interactions / feedback / quality issues
   |      - knowledge corrections / retrieval hints
   |      - evaluation cases / runs / results
   |      - audit logs / admin settings
   |
   +--> [Object/File Storage]
   |      - original files
   |      - extracted.txt / normalized.txt
   |      - page images / OCR output
   |
   +--> [Queue]
   |      - document ingestion
   |      - OCR/layout jobs
   |      - wiki generation
   |      - feedback classification
   |      - index rebuild
   |
   +--> [.NET Worker Service]
   |      - long-running jobs
   |      - retry / dead-letter / status update
   |
   +--> [Vector DB]
   |      - ChromaDB for dev/test
   |      - Qdrant candidate for production
   |
   +--> [Keyword Search]
   |      - PostgreSQL/SQL Server full-text first
   |      - OpenSearch/Elasticsearch if search becomes core at large scale
   |
   +--> [AI Provider Gateway]
   |      - OpenAI-compatible API
   |      - Azure OpenAI / OpenAI / local provider
   |      - model routing / prompt versioning / usage tracking
   |
   +--> [Observability]
          - structured logs
          - traces
          - metrics
          - alerts
```

## 5. Cong nghe de xuat

### 5.1 Giu lai

- ASP.NET Core API.
- Vue frontend.
- EF Core.
- Vector-store adapter boundary.
- AI provider adapter boundary.
- SQLite ledger concept cho knowledge chunks, nhung chuyen DB backend sang PostgreSQL/SQL Server.
- Mock AI services cho test offline.

### 5.2 Nen them cho product

- PostgreSQL hoac SQL Server cho metadata production.
- .NET Worker Service cho background jobs.
- Queue: Hangfire, RabbitMQ, Azure Service Bus, hoac SQL-backed queue tuy moi truong.
- OpenTelemetry cho logs, traces, metrics.
- Microsoft Entra ID/OIDC neu cong ty/khach hang dung Microsoft 365.
- Playwright cho E2E tests.
- Malware scanning cho upload.
- PII/secret detection pipeline truoc khi index.
- Object storage hoac network storage co backup chuan.

### 5.3 Nen them khi can scale

- Qdrant cho vector DB production.
- OpenSearch/Elasticsearch neu keyword search va browse document tro thanh workflow chinh.
- OCR/layout provider cho scanned PDF va anh trong tai lieu.
- Dedicated reranker model neu retrieval quality can tang manh.
- Redis cache neu can cache session, rate limit, short-lived retrieval state.

### 5.4 Chua can them ngay

- Python service rieng.
- Fine-tuning.
- Knowledge graph phuc tap.
- LLM agent tu dong sua tri thuc.
- Multi-region deployment.

## 6. AI va retrieval architecture

### 6.1 Model routing

He thong nen co `IAiTaskRouter` hoac cau hinh tuong duong de chon model theo tac vu:

- Answer generation: model manh, reasoning configurable.
- Wiki generation: model manh, structured output.
- Failure classification: model nho/nhanh hoac rule-based.
- Document understanding: model nho/nhanh, JSON schema.
- Reranking: rule reranker truoc, LLM/dedicated reranker sau.
- Embedding: semantic embedding that trong production.

### 6.2 Prompt va schema governance

Can luu version cho:

- Answer prompt.
- Wiki prompt.
- Document understanding prompt.
- Failure classification prompt.
- Reranking prompt neu co.

Moi `ai_interaction` nen luu:

- model id.
- prompt version.
- retrieval pipeline version.
- embedding model.
- vector collection name.
- context chunk ids.
- confidence va citation ids.

### 6.3 Retrieval pipeline target

```text
User question
-> query understanding
-> build permission/scope filter tu DB
-> vector search
-> keyword/full-text search
-> merge candidates
-> revalidate permission/status/current version tu DB
-> rerank
-> context packing
-> answer generation
-> citation validation
-> store interaction
```

Priority context:

1. Approved correction.
2. Published wiki.
3. Current indexed document.

### 6.4 Evaluation gate

Can co eval suite theo tung tenant/team:

- Golden questions.
- Expected answer summary.
- Expected citations/source ids.
- Forbidden sources neu can.
- Required keywords.
- Scoring rubric.

Moi lan thay doi model, prompt, chunking, reranker, vector DB, hoac embedding model phai chay eval.

## 7. Xu ly anh va scanned document

Chua can Python de xu ly anh. Nen them theo thu tu:

1. OCR provider:
   - Azure AI Document Intelligence neu dung Microsoft stack.
   - AWS Textract hoac Google Document AI neu ha tang phu hop.
   - Tesseract neu bat buoc local/self-host va chap nhan chat luong/bao tri.
2. Page image extraction:
   - PDF page render.
   - DOCX embedded image extraction.
3. Layout-aware ingestion:
   - text, table, key-value, page number, bounding box.
4. Vision caption:
   - tao mo ta cho diagram/screenshot quan trong.
5. Reviewer validation:
   - OCR/vision output co warning neu confidence thap.

Pipeline de xuat:

```text
Upload PDF/DOCX/image
-> extract text normally
-> extract pages/images
-> OCR/layout analysis
-> vision caption for selected images
-> combine text + OCR + captions
-> normalize
-> section/page-aware chunk
-> embedding
-> index with page/image metadata
```

Metadata can them:

- page_number.
- image_id.
- image_caption.
- ocr_confidence.
- bounding_box.
- layout_type: paragraph/table/key_value/figure.

## 8. Security va compliance

### 8.1 Identity va access

- Them SSO OIDC/SAML tuy khach hang.
- Giu local auth chi cho dev/break-glass admin.
- Ho tro mapping group/team tu IdP.
- Session/token policy ro: expiry, refresh, revoke.
- Role Admin/Reviewer/User co the mo rong thanh permission-based policy.

### 8.2 Data protection

- Encrypt database at rest theo ha tang.
- Encrypt storage at rest.
- TLS bat buoc.
- Khong log raw file content, prompt day du, API key, password.
- Secrets nam trong environment/server secret store, khong nam trong repo.
- Cau hinh retention cho AI interactions va logs.

### 8.3 Upload safety

- Malware scan truoc khi approve/index.
- File type sniffing, khong chi dua vao extension.
- Size limit theo tenant.
- Quarantine file loi.
- Block archive nested nguy hiem neu sau nay ho tro zip.

### 8.4 AI data safety

- PII/secret detection truoc khi index.
- Policy cho confidential documents.
- Company-wide publish bat buoc confirm va audit.
- Prompt injection guard:
  - Tai lieu la data, khong phai instruction.
  - System prompt khong duoc bi override boi document content.
  - Citation chi tu backend accepted chunks.

### 8.5 Audit

Audit toi thieu:

- Login failed/success neu policy yeu cau.
- Document upload/download/view.
- Document approve/reject.
- Permission changes.
- AI provider settings changes.
- Wiki generate/publish/reject/edit/version restore.
- Correction approve/reject.
- Rebuild index.
- Admin data reset.

## 9. Operations va reliability

### 9.1 Deployment environments

Can co:

- Development.
- Staging.
- Production.

Staging phai co:

- Data synthetic hoac anonymized.
- Realistic vector DB.
- Real AI provider config rieng.
- Eval suite.

### 9.2 Health checks

API health:

- DB connection.
- storage writable.
- vector DB reachable.
- queue reachable.
- AI provider test optional.

Worker health:

- queue polling.
- failed job count.
- oldest pending job age.
- dead-letter count.

### 9.3 Backup va restore

Can backup dong bo:

- relational DB.
- file/object storage.
- vector DB hoac ledger de rebuild.
- app configuration.

Bat buoc co restore drill:

- Restore DB + storage tren may/moi truong khac.
- Rebuild vector index tu ledger.
- Chay smoke test sau restore.

### 9.4 Observability

Metrics nen co:

- request latency/error rate.
- document processing duration.
- indexing success/failure.
- queue depth.
- AI request latency/cost/token usage.
- answer feedback correct/incorrect.
- eval pass rate.
- permission reject count.
- vector query latency.

Logs nen structured JSON de search duoc theo:

- correlation id.
- user id.
- tenant id.
- document id.
- job id.
- interaction id.

## 10. Product features can them

### 10.1 Reviewer workflow

- In-app notification cho queue can xu ly.
- SLA cho pending review.
- Bulk approve/reject neu policy cho phep.
- Assignment reviewer theo folder/team.
- Comment thread tren document/wiki/correction.

### 10.2 Wiki product

- Wiki editor trong app.
- Wiki versioning.
- Draft compare.
- Publish workflow.
- Archive/restore.
- Related wiki/documents.
- Browse/search wiki.

### 10.3 Knowledge lifecycle

- Owner cho document/wiki.
- Review date / expiry date.
- Warning tai lieu cu.
- Duplicate detection.
- Conflict detection giua documents/wiki.
- Knowledge gap dashboard.

### 10.4 Admin/commercial

- Tenant/customer management neu ban SaaS/multi-customer.
- License key hoac subscription plan neu ban on-prem.
- Feature flags.
- Usage dashboard.
- Export audit/compliance report.
- Admin setup wizard.

## 11. Multi-tenant va deployment model

Co 3 cach ban/deploy:

### 11.1 Single-tenant on-prem

Phu hop khach hang yeu cau data nam trong noi bo.

- Moi khach hang mot deployment rieng.
- DB, storage, vector DB rieng.
- Don gian ve data isolation.
- Van hanh nang hon.

### 11.2 Single-tenant managed cloud

Phu hop B2B enterprise.

- Moi khach hang mot environment rieng tren cloud.
- Vendor quan ly update/backup.
- Data isolation tot.

### 11.3 Multi-tenant SaaS

Chi nen lam sau khi product on dinh.

- Tenant id o moi bang.
- Tenant-aware authorization.
- Per-tenant encryption/config/model policy.
- Strong isolation tests.
- Billing/usage tracking.

Khuyen nghi: bat dau voi single-tenant on-prem hoac managed cloud. Chua nen lam multi-tenant SaaS ngay.

## 12. Roadmap chi tiet

### Phase A - Pilot hardening

Muc tieu: cho 2-3 team noi bo test on dinh.

Cong viec:

- Fix cau hinh local/test embedding provider.
- Them Playwright E2E cho luong login/upload/approve/index/ask/feedback/wiki.
- Them production AI provider setup guide.
- Them smoke script cho staging.
- Them server runbook: start/stop/restart, backup, restore, rebuild index.
- Xac nhan storage nam ngoai web root.
- Xac nhan seed disabled trong production.
- Them dashboard loi processing/indexing.

Acceptance:

- Backend/frontend tests pass.
- E2E pass.
- Smoke MVP pass tren staging.
- 30-100 tai lieu/team duoc ingest.
- Khong co permission leak trong test.

### Phase B - Enterprise identity va operations

Muc tieu: dung that trong cong ty.

Cong viec:

- Them SSO OIDC voi Microsoft Entra ID hoac IdP tuong duong.
- Group/team mapping tu IdP.
- Break-glass admin local.
- OpenTelemetry instrumentation.
- Structured logging.
- Health check dashboard.
- Alerting cho failed jobs, vector DB down, AI provider down.
- Backup schedule va restore drill.

Acceptance:

- User login bang SSO.
- Admin gan role/team theo group.
- Monitoring thay duoc API/worker/DB/vector health.
- Restore drill thanh cong.

### Phase C - Production data platform

Muc tieu: bo gioi han SQLite va API-hosted jobs.

Cong viec:

- Chuyen SQLite sang PostgreSQL hoac SQL Server.
- Tao migration strategy.
- Tach worker service.
- Them queue.
- Chuyen keyword index sang DB full-text search neu phu hop.
- Them dead-letter/retry policy.
- Them idempotency cho jobs.

Acceptance:

- API va worker deploy rieng.
- Job restart khong tao duplicate chunks sai.
- DB backup/restore on dinh.
- Search van dung permission.

### Phase D - Security va compliance

Muc tieu: xu ly du lieu noi bo nhay cam an toan hon.

Cong viec:

- Malware scanning.
- PII/secret detection.
- File type sniffing.
- Policy cho confidential documents.
- Audit document view/download.
- Retention config cho interaction/logs.
- Admin security checklist trong UI.

Acceptance:

- File nguy hiem bi quarantine.
- Secret/PII warning hien cho reviewer truoc khi index/publish.
- Audit truy vet duoc ai xem/tai/sua/publish.

### Phase E - Knowledge product UX

Muc tieu: reviewer va user dung hang ngay.

Cong viec:

- Wiki editor.
- Wiki versioning.
- Wiki browse/search.
- Document preview PDF/text.
- Citation click to document section/page.
- Notification cho reviewer.
- Knowledge gap dashboard.
- Owner/review date/expiry date.

Acceptance:

- Reviewer co the sua wiki truoc/sau publish.
- User click citation de xem nguon.
- Admin thay tai lieu/wiki sap het han review.

### Phase F - Advanced ingestion

Muc tieu: xu ly tai lieu anh/scanned/table tot hon.

Cong viec:

- Them OCR/layout provider.
- Extract image tu DOCX/PDF.
- OCR scanned PDF.
- Table extraction.
- Vision caption cho screenshot/diagram.
- Page/image metadata trong citation.

Acceptance:

- Scanned PDF co the index neu OCR confidence dat nguong.
- Citation hien page number.
- Reviewer thay warning khi OCR confidence thap.

### Phase G - Production vector/search

Muc tieu: scale retrieval.

Cong viec:

- Danh gia Qdrant voi dataset thuc.
- Tao adapter Qdrant.
- Migration/rebuild vector collection tu ledger.
- Payload indexes/filter fields.
- Hybrid retrieval production.
- Optional OpenSearch/Elasticsearch neu keyword search can manh.

Acceptance:

- Rebuild index sang Qdrant tu ledger.
- Retrieval latency va quality dat nguong.
- Permission filter duoc test voi dataset lon.

### Phase H - Commercialization

Muc tieu: co the ban.

Cong viec:

- Packaging deployment: on-prem installer/script hoac managed cloud template.
- Tenant/customer config.
- License/subscription mechanism neu can.
- Admin onboarding wizard.
- Product documentation: admin, reviewer, user, security, API, deployment.
- Support process va issue templates.
- Demo dataset va demo script.
- Versioned release notes.

Acceptance:

- Cai dat moi tren environment sach theo docs.
- Demo end-to-end trong 30 phut.
- Co support playbook cho loi thuong gap.
- Co security overview gui cho khach hang.

## 13. Thu tu uu tien 90 ngay

### 0-30 ngay

- Fix config embedding local/test.
- Playwright E2E.
- Production AI provider runbook.
- Staging smoke.
- SSO discovery va design.
- Monitoring/logging co ban.

### 31-60 ngay

- SSO implementation.
- PostgreSQL/SQL Server migration spike.
- Worker/queue design va proof of concept.
- Wiki editor MVP.
- Backup/restore drill.
- Malware scan proof of concept.

### 61-90 ngay

- Chuyen DB production.
- Tach worker.
- Security scan pipeline.
- Wiki versioning.
- OCR/layout provider spike.
- Evaluation gate trong CI/staging.

## 14. Definition of Done cho Product Beta

Product Beta dat khi:

- SSO hoat dong hoac co quyet dinh tam dung ro.
- Real AI provider cau hinh duoc bang secret/env.
- Local/test van chay duoc bang mock.
- Smoke va E2E pass tren staging.
- Backup/restore drill da chay.
- Monitoring co API/worker/DB/vector/AI health.
- Admin co runbook.
- 2-3 team dung duoc voi du lieu that co gioi han.
- Khong co loi permission leak nghiem trong.

## 15. Definition of Done cho Product v1

Product v1 dat khi:

- Metadata DB la PostgreSQL hoac SQL Server.
- Worker tach khoi API.
- Queue co retry/dead-letter.
- SSO production-ready.
- Malware scan va file safety co ban.
- Wiki editor/versioning co workflow approve.
- Evaluation gate bat buoc truoc khi doi prompt/model/retrieval.
- Audit du cho hanh dong business chinh.
- Restore drill lap lai duoc.
- Tai lieu admin/reviewer/user/deployment/security day du.

## 16. Definition of Done cho Commercial v2

Commercial v2 dat khi:

- Co deployment model ro: on-prem, managed cloud, hoac multi-tenant SaaS.
- Co customer onboarding checklist.
- Co release/versioning policy.
- Co security overview va data handling policy.
- Co usage/health dashboard cho admin.
- Co support workflow.
- Co kha nang cau hinh provider/model theo customer.
- Co index rebuild/migration strategy khi doi vector DB/embedding model.

## 17. Rui ro va cach giam

### Permission leak

Giam bang:

- Build filter tu relational DB.
- Vector DB chi la index, khong la source of truth.
- Revalidate moi chunk truoc prompt.
- E2E va unit test voi user khong co quyen.

### Hallucination

Giam bang:

- Structured output.
- Citation validation.
- Confidence/missing info.
- Eval suite.
- Feedback/correction loop.

### Chi phi AI tang

Giam bang:

- Model routing.
- Cache embeddings theo text hash.
- Batch embeddings.
- Gioi han context packing.
- Theo doi token usage theo team/tenant.

### Latency cao

Giam bang:

- Worker cho viec nang.
- Rerank toi uu.
- Cache retrieval/embedding hop ly.
- Streaming answer neu can.

### Tai lieu dau vao kem

Giam bang:

- Content preparation guide.
- OCR confidence warning.
- Reviewer ownership.
- Knowledge quality dashboard.

### Commercial scope creep

Giam bang:

- Bat dau single-tenant.
- Chua lam multi-tenant SaaS den khi product v1 on dinh.
- Feature flags cho tinh nang enterprise.

## 18. Cac quyet dinh can chot

Can chot som:

- DB production: PostgreSQL hay SQL Server.
- Identity provider dau tien: Microsoft Entra ID hay IdP khac.
- Deployment model dau tien: on-prem hay managed cloud.
- AI provider production: Azure OpenAI, OpenAI, hay local/openai-compatible.
- Vector DB production: tiep tuc Chroma trong thoi gian ngan hay chuyen Qdrant o v1/v2.
- OCR provider: Azure AI Document Intelligence hay provider khac.
- Data retention policy cho interactions/logs.
- Khach hang dau tien la internal-only hay external pilot.

## 19. Khuyen nghi cuoi

Huong nen di:

1. Lam Product Beta that chac cho noi bo.
2. Chuyen nen tang production: SSO, DB, worker, observability, backup.
3. Nang UX reviewer/wiki va security.
4. Sau do moi dau tu Qdrant/OCR/advanced search neu du lieu va khach hang chung minh nhu cau.

Khong nen:

- Them Python chi vi AI. .NET hien tai du goi AI provider, xu ly queue, indexing, OCR API va production backend.
- Lam multi-tenant SaaS qua som.
- Cho AI tu dong publish/sua tri thuc khong co reviewer.
- Doi vector DB truoc khi co eval benchmark va dataset thuc.

