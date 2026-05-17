# Technical system overview for team and AI agents

Tài liệu này là bản mô tả kỹ thuật tập trung của hệ thống Internal Knowledge Copilot. Mục tiêu là giúp team kỹ thuật hoặc AI coding agent hiểu nhanh hệ thống hiện tại, biết nên đọc code ở đâu, luồng dữ liệu đi như thế nào, và những nguyên tắc nào không được phá vỡ khi phát triển tiếp.

Nếu chỉ có thời gian đọc một tài liệu kỹ thuật trước khi trao đổi hoặc sửa code, hãy đọc file này trước.

## 1. Hệ thống này là gì?

Internal Knowledge Copilot là một hệ thống quản trị tri thức nội bộ có AI hỗ trợ. Hệ thống cho phép user upload tài liệu, reviewer duyệt tài liệu, backend xử lý tài liệu thành knowledge chunks, lưu embedding vào vector DB, hỗ trợ AI Q&A có citations, sinh wiki draft từ tài liệu đã duyệt, và publish wiki thành nguồn tri thức chuẩn hóa.

Mô tả ngắn:

```text
Document management
+ review workflow
+ document processing
+ RAG Q&A
+ wiki generation/publishing
+ audit/feedback/KPI
```

Có thể xem hệ thống có một lớp giống AI Harness bên trong, nhưng sản phẩm đầy đủ không chỉ là AI Harness. Nó là một Enterprise RAG Knowledge Assistant.

## 2. Stack hiện tại

Backend:

- ASP.NET Core API.
- Entity Framework Core.
- SQLite là metadata database.
- Hosted background worker xử lý job.

Frontend:

- Vue 3.
- TypeScript.
- Vite.

Vector database:

- ChromaDB là runtime hiện tại.
- Qdrant từng là hướng kiến trúc ban đầu/future option, nhưng implementation hiện tại dùng Chroma sau boundary `IKnowledgeVectorStore`.

AI provider:

- Có mock provider để chạy local/test.
- Có OpenAI-compatible provider để dùng model thật.
- Các use case chính: embedding, answer generation, document understanding, wiki draft generation.

File storage:

- Local filesystem.
- File upload không nằm trong public web root.
- Download phải đi qua API có kiểm tra quyền.

Deployment target:

- Windows Server / IIS.

## 3. Những thư mục code cần biết

```text
src/backend/InternalKnowledgeCopilot.sln
src/backend/InternalKnowledgeCopilot.Api
src/backend/InternalKnowledgeCopilot.Tests
src/frontend
docs/technical
scripts
```

Backend module chính:

```text
src/backend/InternalKnowledgeCopilot.Api/Modules
```

Infrastructure chính:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Database
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/FileStorage
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/DocumentProcessing
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/AiProvider
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/VectorStore
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/KnowledgeIndex
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/KeywordSearch
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/BackgroundJobs
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Audit
```

Frontend API wrappers:

```text
src/frontend/src/api
```

## 4. Source of truth

SQLite là source of truth cho nghiệp vụ:

- users
- roles
- teams
- folders
- folder permissions
- documents
- document versions
- processing jobs
- wiki drafts
- wiki pages
- AI interactions
- AI feedback
- audit logs
- knowledge chunk ledger
- keyword index

Vector DB không phải source of truth về quyền. Vector DB chỉ hỗ trợ retrieval nhanh bằng embedding và metadata filter.

Quy tắc quan trọng:

```text
Không được dùng chunk từ vector DB để trả lời nếu backend chưa kiểm tra quyền và trạng thái nguồn theo SQLite.
```

## 5. Domain model cốt lõi

Document:

- Đại diện tài liệu nghiệp vụ user upload.
- Có folder, title, description, status, current version.

DocumentVersion:

- Đại diện từng file version của một document.
- Có stored file path, extracted text path, normalized text path, status, summary, language, document type, text hash, indexed time.

ProcessingJob:

- Job nền để xử lý document hoặc các tác vụ async khác.
- Với document upload, job chính là `ExtractAndEmbedDocument`.

KnowledgeChunk:

- Ledger trong SQLite của chunks đã index.
- Giúp audit/rebuild/inspect knowledge source.

KnowledgeChunkIndex:

- Keyword index trong SQLite.
- Dùng bổ sung cho vector retrieval.

WikiDraft:

- Bản nháp AI sinh từ document version đã indexed.
- Chưa phải tri thức chính thức cho đến khi reviewer publish.

WikiPage:

- Wiki đã publish.
- Được chunk/embed/index riêng.
- Là nguồn tri thức ưu tiên hơn raw document chunks khi Q&A.

## 6. Trạng thái quan trọng

Document status:

```text
PendingReview
Approved
Rejected
Archived
Deleted
```

DocumentVersion status:

```text
PendingReview
Approved
Processing
Indexed
Rejected
ProcessingFailed
```

ProcessingJob status:

```text
Pending
Running
Succeeded
Failed
```

Wiki status:

```text
Draft
Published
Rejected
Archived
```

Điều kiện để một document version được xem là usable knowledge:

```text
Document.DeletedAt == null
Document.Status == Approved
Document.CurrentVersionId == version.Id
DocumentVersion.Status == Indexed
User có quyền với folder/document
Chunks đã có trong vector DB
Chunks đã có trong SQLite ledger/keyword index
```

## 7. Luồng upload document đến knowledge

Endpoint:

```http
POST /api/documents
```

Code chính:

```text
Modules/Documents/DocumentsController.cs
DocumentsController.Upload(...)
DocumentsController.CreateVersionAsync(...)
```

Luồng:

1. Lấy user từ JWT claim bằng `GetCurrentUserId()`.
2. Validate file bằng `FileUploadValidator.Validate(...)`.
3. Kiểm tra quyền folder bằng `IFolderPermissionService.CanViewFolderAsync(...)`.
4. Validate title.
5. Tạo `DocumentEntity` với `Status = PendingReview`.
6. Tạo `DocumentVersionEntity` với `Status = PendingReview`.
7. Lưu file bằng `FileStorageService.SaveDocumentVersionAsync(...)`.
8. Ghi `Documents` và `DocumentVersions` vào SQLite.
9. Ghi audit `DocumentUploaded`.

Sau upload, tài liệu chưa thành tri thức. Nó phải được reviewer approve và worker index.

## 8. Luồng approve và background job

Endpoint:

```http
POST /api/documents/{id}/approve
```

Code chính:

```text
DocumentsController.Approve(...)
Infrastructure/BackgroundJobs/ProcessingJobWorker.cs
ProcessingJobWorker.ExecuteAsync(...)
ProcessingJobWorker.ProcessNextJobAsync(...)
```

Luồng approve:

1. Reviewer/Admin approve một `DocumentVersion`.
2. Version chuyển `Approved`.
3. Document chuyển `Approved`.
4. `Document.CurrentVersionId` trỏ vào version được approve.
5. Tạo `ProcessingJobEntity`:

```text
JobType = ExtractAndEmbedDocument
TargetType = DocumentVersion
TargetId = version.Id
Status = Pending
```

Luồng worker:

1. Poll job `Pending`.
2. Set job `Running`.
3. Nếu job là `ExtractAndEmbedDocument`, gọi:

```text
IDocumentProcessingService.ProcessDocumentVersionAsync(job.TargetId, ...)
```

4. Thành công thì job `Succeeded`.
5. Lỗi thì retry, hết retry thì job `Failed` và version `ProcessingFailed`.

## 9. Luồng document processing

Code chính:

```text
Infrastructure/DocumentProcessing/DocumentProcessingService.cs
DocumentProcessingService.ProcessDocumentVersionAsync(...)
```

Các dependency quan trọng:

```text
DocumentTextExtractor
DocumentTextNormalizer
SectionDetector
TextChunker
IEmbeddingService
IDocumentUnderstandingService
IKnowledgeVectorStore
IKnowledgeChunkLedgerService
IKnowledgeKeywordIndexService
```

Luồng xử lý:

1. Load `DocumentVersion`, `Document`, `Folder`.
2. Set version `Processing`.
3. Extract text:

```text
DocumentTextExtractor.ExtractAsync(...)
```

Supported extensions:

```text
.txt
.md
.markdown
.docx
.pdf
```

4. Ghi `extracted.txt`.
5. Normalize text:

```text
DocumentTextNormalizer.Normalize(...)
```

6. Ghi `normalized.txt`.
7. Tính `TextHash` bằng SHA256 trên normalized text.
8. Detect sections:

```text
SectionDetector.Detect(...)
```

9. Phân tích document:

```text
IDocumentUnderstandingService.AnalyzeAsync(...)
```

10. Chunk text:

```text
TextChunker.Chunk(...)
```

11. Tạo embedding cho từng chunk:

```text
IEmbeddingService.CreateEmbeddingAsync(...)
```

12. Tạo `KnowledgeChunkRecord` với metadata `source_type=document`.
13. Upsert chunks vào Chroma:

```text
ChromaKnowledgeVectorStore.UpsertChunksAsync(...)
```

14. Ghi SQLite ledger:

```text
KnowledgeChunkLedgerService.ReplaceChunksAsync(...)
```

15. Ghi keyword index:

```text
KnowledgeKeywordIndexService.ReplaceChunksAsync(...)
```

16. Set version `Indexed`.

## 10. Luồng AI Q&A

Endpoint Q&A nằm ở module AI.

Code chính:

```text
Modules/Ai/AiController.cs
Modules/Ai/AiQuestionService.cs
```

Luồng tổng quát:

1. User gửi câu hỏi.
2. Backend validate scope:

```text
All
Folder
Document
```

3. Kiểm tra quyền user với folder/document.
4. Tạo embedding cho câu hỏi.
5. Query vector DB qua `IKnowledgeVectorStore.QueryAsync(...)`.
6. Query keyword index qua `IKnowledgeKeywordIndexService.SearchAsync(...)`.
7. Merge vector results và keyword results.
8. Chuyển metadata thành retrieved chunks.
9. Recheck quyền và trạng thái nguồn theo SQLite.
10. Loại chunks không hợp lệ:

```text
document không approved/current/indexed
wiki không published
folder không visible
```

11. Rerank chunks.
12. Ưu tiên wiki published hơn raw document chunks khi phù hợp.
13. Đóng gói context.
14. Gọi answer generation service.
15. Trả answer kèm citations.
16. Lưu AI interaction và citations.

Tài liệu chi tiết:

```text
docs/technical/AI_QUESTION_TO_ANSWER_FLOW.md
```

## 11. Luồng wiki generation và publishing

Code chính:

```text
Modules/Wiki/WikiService.cs
WikiService.GenerateDraftAsync(...)
WikiService.PublishAsync(...)
WikiService.IndexWikiPageAsync(...)
```

Generate draft:

1. Reviewer chọn document version đã `Indexed`.
2. `GenerateDraftAsync` kiểm tra:

```text
version.Status == Indexed
document.CurrentVersionId == version.Id
reviewer có quyền folder
```

3. Đọc source text từ `NormalizedTextPath`, fallback `ExtractedTextPath`.
4. Tìm related documents qua `FindRelatedDocumentsAsync(...)`.
5. Gọi:

```text
IWikiDraftGenerationService.GenerateAsync(...)
```

6. Tạo `WikiDraftEntity` với `Status = Draft`.
7. Ghi audit `WikiDraftGenerated`.

Publish:

1. Reviewer publish draft.
2. `PublishAsync` validate draft còn `Draft`.
3. Tạo `WikiPageEntity`.
4. Set draft `Published`.
5. Gọi `IndexWikiPageAsync(...)`.
6. Wiki content được detect section, chunk, embed.
7. Upsert vector chunks với metadata `source_type=wiki`, `status=published`.
8. Ghi ledger và keyword index cho wiki.
9. Ghi audit `WikiPublished`.

Tài liệu chi tiết:

```text
docs/technical/DOCUMENT_UPLOAD_TO_KNOWLEDGE_FLOW.md
docs/technical/SIMULATED_TEXT_TO_KNOWLEDGE_FLOW.md
```

## 12. AI provider boundary

AI provider code nằm ở:

```text
Infrastructure/AiProvider
```

Các abstraction chính:

```text
IEmbeddingService
IAnswerGenerationService
IDocumentUnderstandingService
IWikiDraftGenerationService
```

Runtime service chọn mock hoặc OpenAI-compatible implementation:

```text
RuntimeEmbeddingService
RuntimeAnswerGenerationService
RuntimeDocumentUnderstandingService
RuntimeWikiDraftGenerationService
```

Nguyên tắc:

- Feature code không nên gọi thẳng HTTP model provider nếu đã có abstraction.
- Test/local có thể dùng mock provider.
- Provider thật nên đi qua OpenAI-compatible client/settings.

## 13. Permission và security rules

Các nguyên tắc không được phá:

1. User chỉ thấy folder/document họ có quyền.
2. Upload vào folder cũng phải kiểm tra quyền.
3. Download file phải đi qua authorized API.
4. File upload không được serve trực tiếp từ public web root.
5. Reviewer/Admin mới được approve/reject document và generate/publish wiki.
6. Vector metadata chỉ là filter phụ, không phải nguồn sự thật về quyền.
7. Trước khi đưa chunk vào prompt, backend phải recheck quyền và trạng thái nguồn.
8. Company-wide wiki publish cần confirmation.
9. Chỉ `Approved + Current + Indexed` document version được dùng cho Q&A.
10. Chỉ `Published` wiki được dùng cho Q&A.

## 14. Các command verification

Backend:

```powershell
dotnet restore src/backend/InternalKnowledgeCopilot.sln
dotnet build src/backend/InternalKnowledgeCopilot.sln
dotnet test src/backend/InternalKnowledgeCopilot.sln
```

Frontend:

```powershell
cd src/frontend
npm install
npm run build
npm test
```

Smoke MVP:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-mvp.ps1
```

## 15. Cách đọc repo khi bắt đầu task mới

Nếu là AI agent hoặc developer mới, đọc theo thứ tự:

1. `AI_HANDOFF.md`
2. `docs/technical/TECHNICAL_SYSTEM_OVERVIEW_FOR_TEAM_AND_AI.md`
3. `PHASE_STATUS.md`
4. `KNOWN_LIMITATIONS.md`
5. Tài liệu flow liên quan:

```text
docs/technical/DOCUMENT_UPLOAD_TO_KNOWLEDGE_FLOW.md
docs/technical/AI_QUESTION_TO_ANSWER_FLOW.md
docs/technical/SIMULATED_TEXT_TO_KNOWLEDGE_FLOW.md
```

6. API/data model nếu cần:

```text
API_SPEC.md
DATA_MODEL.md
RAG_AND_WIKI_FLOW.md
```

7. Code module liên quan.
8. Tests liên quan.

## 16. Những điểm dễ nhầm

Chroma và Qdrant:

- Implementation hiện tại dùng ChromaDB.
- Nếu tài liệu cũ nhắc Qdrant, hiểu đó là định hướng/future option hoặc lịch sử kiến trúc.
- Không hard-code trực tiếp vào Chroma ở feature code mới nếu có thể dùng `IKnowledgeVectorStore`.

Approved không đồng nghĩa Indexed:

- Reviewer approve chỉ tạo processing job.
- AI Q&A chỉ nên dùng document khi version đã `Indexed`.

Wiki draft không phải wiki knowledge:

- Draft do AI sinh vẫn cần reviewer publish.
- Chỉ wiki page published mới được index và dùng như nguồn tri thức chính thức.

Vector DB không đủ để enforce permission:

- Metadata filter giúp giảm kết quả.
- SQLite vẫn là nơi quyết định quyền và trạng thái hợp lệ.

File gốc không phải runtime knowledge:

- Q&A không đọc trực tiếp file gốc.
- Runtime knowledge nằm trong vector chunks, ledger, keyword index, và metadata đã kiểm tra.

## 17. Tóm tắt kiến trúc một dòng

```text
Vue UI -> ASP.NET Core API -> SQLite + Local Storage + Background Processing -> Chroma Vector DB + AI Provider -> RAG Q&A / Wiki Knowledge
```

## 18. Tóm tắt trách nhiệm theo layer

Frontend:

- Gọi API.
- Hiển thị tài liệu, review queue, Q&A, wiki, feedback, dashboard.

API modules:

- Điều phối use case.
- Validate request.
- Kiểm tra quyền.
- Ghi audit.

Infrastructure:

- Database access.
- File storage.
- Background jobs.
- Document processing.
- AI provider.
- Vector store.
- Knowledge ledger.
- Keyword search.

SQLite:

- Source of truth nghiệp vụ.

Chroma:

- Semantic retrieval.

AI provider:

- Embedding.
- Understanding.
- Answer generation.
- Wiki draft generation.

Reviewer workflow:

- Biến file upload chưa tin cậy thành document được duyệt.
- Biến wiki draft thành tri thức published.

## 19. Tài liệu liên quan

```text
AI_HANDOFF.md
ARCHITECTURE_MVP.md
API_SPEC.md
DATA_MODEL.md
RAG_AND_WIKI_FLOW.md
docs/technical/DOCUMENT_UPLOAD_TO_KNOWLEDGE_FLOW.md
docs/technical/AI_QUESTION_TO_ANSWER_FLOW.md
docs/technical/SIMULATED_TEXT_TO_KNOWLEDGE_FLOW.md
docs/technical/SMART_AI_UPGRADE_PLAN.md
docs/technical/PRODUCTIZATION_PLAN.md
```
