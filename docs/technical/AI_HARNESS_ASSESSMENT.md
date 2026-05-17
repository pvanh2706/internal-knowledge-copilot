# Đánh giá dự án theo góc nhìn AI Harness

Tài liệu này phân tích dự án Internal Knowledge Copilot dựa trên source code, cấu hình, database schema, README, API, service, prompt và luồng xử lý hiện có. Mục tiêu là đánh giá hệ thống đang giống AI Harness ở mức nào.

Nguyên tắc phân tích:

- Không kết luận nếu không có bằng chứng trong source code hoặc tài liệu dự án.
- Nếu chưa thấy implementation, ghi rõ: "Chưa thấy trong source code".
- Nếu có nhiều khả năng, ghi rõ giả định.
- Ưu tiên các phần liên quan LLM, RAG, embedding, vector database, prompt, retrieval, logging, evaluation, permission và tool calling.

Các nguồn đã kiểm tra chính:

- `README.md`
- `AI_HANDOFF.md`
- `RAG_AND_WIKI_FLOW.md`
- `docs/technical/DOCUMENT_UPLOAD_TO_KNOWLEDGE_FLOW.md`
- `docs/technical/AI_QUESTION_TO_ANSWER_FLOW.md`
- `src/backend/InternalKnowledgeCopilot.Api`
- `src/frontend/src`
- `src/backend/InternalKnowledgeCopilot.Tests`

## 1. Tổng quan dự án

Tên dự án xác định được:

- `Internal Knowledge Copilot`, theo `README.md`, `AI_HANDOFF.md`, tên solution/backend và namespace.

Dự án dùng để làm gì:

- Quản trị tri thức nội bộ.
- Cho phép upload tài liệu, review/approve tài liệu, xử lý thành chunks, tạo embedding, lưu vào vector DB, hỏi đáp AI có citation, thu thập feedback, sinh wiki draft và publish wiki.

Người dùng chính:

- `User`: upload tài liệu, hỏi AI, gửi feedback.
- `Reviewer`: duyệt/reject tài liệu, generate/publish wiki, xử lý feedback sai, tạo correction, chạy evaluation.
- `Admin`: quản lý user/team/folder/AI settings/audit log.

Bằng chứng:

- Role nằm ở `src/backend/InternalKnowledgeCopilot.Api/Common/UserRole.cs`.
- Auth/role enforcement nằm trong các controller như `DocumentsController`, `WikiController`, `FeedbackController`, `EvaluationController`, `AiSettingsController`, `AuditLogsController`.
- UI route phân quyền nằm ở `src/frontend/src/router/index.ts`.

Vấn đề nghiệp vụ đang giải quyết:

- Tài liệu nội bộ rời rạc, khó tìm, khó kiểm soát chất lượng.
- Rủi ro AI trả lời từ tài liệu chưa duyệt hoặc user không có quyền xem.
- Cần tri thức có nguồn, có reviewer kiểm soát, có feedback và KPI để pilot nội bộ.

Kết quả đầu ra chính của hệ thống:

- Câu trả lời AI có confidence, missing information, conflicts, follow-up suggestions và citations.
- Wiki draft và wiki page published.
- Knowledge chunks trong Chroma, SQLite ledger và SQLite keyword index.
- Feedback, quality issue, correction, evaluation run và dashboard summary.

## 2. Kiến trúc tổng thể

Frontend:

- Vue 3 + TypeScript + Vite.
- API wrappers nằm ở `src/frontend/src/api`.
- Các page chính: `AiQuestionPage.vue`, `RetrievalExplainPage.vue`, `FeedbackReviewPage.vue`, `EvaluationPage.vue`, `KnowledgeIndexPage.vue`, `WikiDraftPage.vue`, `DocumentListPage.vue`, `DashboardPage.vue`.

Backend:

- ASP.NET Core API .NET 8.
- Entry point và DI nằm ở `src/backend/InternalKnowledgeCopilot.Api/Program.cs`.
- Modules nằm trong `src/backend/InternalKnowledgeCopilot.Api/Modules`.

Database:

- SQLite qua Entity Framework Core.
- `AppDbContext` định nghĩa các bảng nghiệp vụ: users, teams, folders, documents, document_versions, processing_jobs, ai_interactions, ai_interaction_sources, ai_feedback, ai_quality_issues, knowledge_corrections, knowledge_chunks, knowledge_chunk_indexes, evaluation_cases, evaluation_runs, wiki_drafts, wiki_pages, audit_logs, ai_provider_settings.

Vector database:

- ChromaDB runtime hiện tại.
- Cấu hình ở `appsettings.json` mục `Chroma`.
- Adapter: `IKnowledgeVectorStore`.
- Implementation: `ChromaKnowledgeVectorStore`.

Embedding model:

- Cấu hình default trong `appsettings.json`: `text-embedding-3-large`, dimension `3072`.
- Có thể cấu hình trong DB qua `AiProviderSettingsService`.
- Có mock embedding 64 chiều trong `MockEmbeddingService`.

LLM provider/model:

- Cấu hình default: provider `mock`, chat model `gpt-5.5`, endpoint mode `chat-completions`.
- Có OpenAI-compatible client hỗ trợ chat completions, responses API và Anthropic messages.
- Code: `OpenAiCompatibleClient`, `AiProviderOptions`, `AiProviderSettingsService`.

File storage:

- Local filesystem, root mặc định `./storage`.
- Cấu hình ở `appsettings.json` mục `Storage`.
- Service: `FileStorageService`.
- Validate upload: `FileUploadValidator`.

Auth/phân quyền:

- Login bằng email/password.
- JWT Bearer.
- Role: Admin, Reviewer, User.
- Folder permission theo team và user override.
- Service: `FolderPermissionService`.

Background job/queue:

- Có bảng `processing_jobs`.
- Hosted service polling: `ProcessingJobWorker`.
- Job hiện thấy:
  - `ExtractAndEmbedDocument`
  - `ClassifyAiFailure`
- Chưa thấy queue broker riêng như RabbitMQ, Hangfire, Azure Queue.

Logging/monitoring:

- Có ASP.NET logging cấu hình trong `appsettings.json`.
- Có audit log nghiệp vụ qua `AuditLogService`.
- Có dashboard KPI qua `DashboardController`.
- Chưa thấy OpenTelemetry, distributed tracing, metrics exporter hoặc centralized log sink trong source code.

Luồng tổng thể:

```text
User
-> Vue Frontend
-> ASP.NET Core Backend API
-> SQLite auth/permission/document metadata
-> Local File Storage
-> Background Processing
-> Document Processing / Embedding Service
-> Chroma Vector DB + SQLite Knowledge Ledger + SQLite Keyword Index
-> AI Q&A Retrieval
-> LLM Provider
-> Response + Citations
-> AI Interaction / Feedback / Evaluation / Audit
```

## 3. Luồng xử lý chính

Luồng từ lúc user nhập câu hỏi đến lúc nhận câu trả lời:

1. User nhập câu hỏi ở frontend:

```text
src/frontend/src/pages/ai/AiQuestionPage.vue
```

2. Frontend gọi:

```http
POST /api/ai/ask
```

API wrapper:

```text
src/frontend/src/api/ai.ts
askQuestion(...)
```

3. Backend nhận ở:

```text
src/backend/InternalKnowledgeCopilot.Api/Modules/Ai/AiController.cs
AiController.Ask(...)
```

4. Controller lấy user id từ JWT claim rồi gọi:

```text
IAiQuestionService.AskAsync(...)
AiQuestionService.AskAsync(...)
```

5. Service validate câu hỏi rỗng và validate scope:

```text
ValidateScopeAsync(...)
```

Scope hỗ trợ:

```text
All
Folder
Document
```

6. Hệ thống có rewrite query không?

- Chưa thấy rewrite query bằng LLM trong source code.
- `UnderstandQuery(...)` chỉ tokenize, normalize và trích keyword thủ công.
- API response có field `RewrittenQuestion`, nhưng trong code hiện tại nó được set bằng chính `question`.

7. Hệ thống tìm tài liệu bằng cách nào?

- Tạo embedding cho question:

```text
embeddingService.CreateEmbeddingAsync(question, ...)
```

- Query vector DB:

```text
vectorStore.QueryAsync(queryEmbedding, SearchLimit, knowledgeFilter, ...)
```

- Query keyword index SQLite:

```text
keywordIndexService.SearchAsync(queryUnderstanding.Keywords, KeywordSearchLimit, knowledgeFilter, ...)
```

- Merge vector candidates và keyword candidates:

```text
MergeCandidateResults(...)
```

8. Có dùng embedding không?

- Có.
- Service: `IEmbeddingService`, `RuntimeEmbeddingService`, `OpenAiCompatibleEmbeddingService`, `MockEmbeddingService`.

9. Có dùng vector database không?

- Có.
- Adapter: `IKnowledgeVectorStore`.
- Implementation: `ChromaKnowledgeVectorStore`.
- Query endpoint Chroma: `.../collections/{id}/query`.

10. Có topK/rerank/score threshold không?

- Có topK/limit:
  - `SearchLimit = 50`
  - `KeywordSearchLimit = 20`
  - `MaxContextChunks = 8`
  - `MaxChunksPerKnowledgeItem = 3`
- Có rerank thủ công:
  - `RerankAndPackContext(...)`
  - `ScoreChunk(...)`
  - `ExplainChunkScore(...)`
- Chưa thấy score threshold cứng để loại candidate theo distance/score trong source code.

11. Có lọc permission không?

- Có hai lớp:
  - Metadata filter trước khi query Chroma qua `KnowledgeQueryFilter`.
  - Recheck bằng SQLite sau retrieval trong `AnalyzeCandidateAccessAsync(...)`.

12. Prompt được tạo ở đâu?

- Answer prompt nằm trong:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/AiProvider/AnswerGenerationService.cs
OpenAiCompatibleAnswerGenerationService.GenerateAsync(...)
```

13. Gọi LLM ở service nào?

- `OpenAiCompatibleAnswerGenerationService.GenerateAsync(...)`
- Gọi `OpenAiCompatibleClient.CompleteAsync(...)`.

14. Response trả về thế nào?

Backend trả:

```text
AskQuestionResponse(
  InteractionId,
  Answer,
  NeedsClarification,
  Confidence,
  MissingInformation,
  Conflicts,
  SuggestedFollowUps,
  Citations
)
```

Frontend hiển thị answer, confidence, missing information, conflicts, follow-ups và citations trong `AiQuestionPage.vue`.

15. Có lưu lịch sử hỏi đáp không?

- Có.
- `AiQuestionService.AskAsync` tạo `AiInteractionEntity` và `AiInteractionSourceEntity`.
- Bảng:
  - `ai_interactions`
  - `ai_interaction_sources`

Chưa thấy trong source code:

- Lưu prompt cuối cùng gửi cho LLM.
- Lưu raw LLM response.
- Lưu model/provider cụ thể được dùng tại từng interaction.

## 4. Nguồn dữ liệu và xử lý tài liệu

Nguồn dữ liệu có implementation:

| Nguồn | Tình trạng | Bằng chứng |
|---|---|---|
| PDF | Có | `DocumentTextExtractor.ExtractPdf(...)`, dùng `UglyToad.PdfPig` |
| Word DOCX | Có | `DocumentTextExtractor.ExtractDocx(...)`, dùng `DocumentFormat.OpenXml` |
| Markdown | Có | `.md`, `.markdown` đọc bằng `File.ReadAllTextAsync` |
| TXT | Có | `.txt` đọc bằng `File.ReadAllTextAsync` |
| Excel | Chưa thấy trong source code | Không thấy `.xls/.xlsx` trong validator/extractor |
| HTML | Chưa thấy trong source code | Không thấy parser HTML |
| Database import | Chưa thấy trong source code | Không thấy connector/import pipeline DB làm knowledge source |
| Issue/ticket | Chưa thấy trong source code | Docs/test có chữ ticket nhưng không có integration |
| Log lỗi | Chưa thấy như source nhập liệu chính thức | Có quality issue/evidence, nhưng không có log ingestion |
| Source code | Chưa thấy trong source code | Không có parser/indexer source code |
| Knowledge correction | Có | `KnowledgeCorrectionEntity`, `AiFeedbackService.ApproveCorrectionAsync(...)` index correction |

Upload/import:

- Document upload:

```text
POST /api/documents
DocumentsController.Upload(...)
```

- Upload version mới:

```text
POST /api/documents/{id}/versions
DocumentsController.UploadVersion(...)
```

Parse nội dung:

- `DocumentTextExtractor.ExtractAsync(...)`
- `.txt/.md/.markdown`: đọc text trực tiếp.
- `.docx`: OpenXml lấy `Text`.
- `.pdf`: PdfPig đọc text từng page.

Chunking:

- `TextChunker.Chunk(...)`
- Target: 2800 characters.
- Overlap: 350 characters.
- Nếu có sections thì chunk theo section.
- Section detection: `SectionDetector.Detect(...)`, nhận Markdown headings, numbered headings và một số heading tiếng Việt.

Metadata lưu:

- Document/version metadata trong `DocumentEntity`, `DocumentVersionEntity`.
- Knowledge chunk metadata trong Chroma, SQLite `KnowledgeChunkEntity`, SQLite `KnowledgeChunkIndexEntity`.
- Metadata gồm source type, source id, document id, version id, wiki page id, folder id, title, folder path, status, visibility, section, char offset, language, document type, keywords, sensitivity, text hash.

Có version tài liệu không?

- Có.
- `DocumentVersionEntity.VersionNumber`.
- `DocumentEntity.CurrentVersionId`.

Có phân quyền theo tài liệu không?

- Quyền chính theo folder.
- User có quyền folder thì thấy document trong folder đó.
- Chưa thấy ACL riêng theo từng document ngoài folder permission trong source code.

Có xóa/cập nhật lại embedding khi tài liệu thay đổi không?

- Khi version mới được approve và indexed, retrieval chỉ dùng current indexed version nhờ recheck SQLite.
- Chưa thấy logic xóa vector cũ khỏi Chroma khi version thay đổi.
- Có ledger và rebuild index từ ledger qua `KnowledgeIndexRebuildService`.
- Rủi ro: vector cũ có thể còn trong Chroma, nhưng `AiQuestionService.AnalyzeCandidateAccessAsync(...)` loại document version không current/indexed trước khi đưa vào prompt.

## 5. RAG pipeline

Cách tạo embedding:

- Document/wiki/correction chunks gọi `IEmbeddingService.CreateEmbeddingAsync(...)`.
- Question cũng gọi `IEmbeddingService.CreateEmbeddingAsync(question, ...)`.
- Runtime chọn mock hoặc OpenAI-compatible theo `AiProviderSettingsService`.

Model embedding đang dùng:

- Default config: `text-embedding-3-large`, dimension `3072`.
- Có thể đổi qua UI/API admin settings.
- Nếu `EmbeddingProviderName` là `mock` hoặc Anthropic/Claude thì dùng `MockEmbeddingService` theo `RuntimeEmbeddingService`.

Vector DB đang dùng:

- ChromaDB.
- Config:

```json
"Chroma": {
  "BaseUrl": "http://localhost:8000",
  "Collection": "knowledge_chunks",
  "Tenant": "default_tenant",
  "Database": "default_database"
}
```

Cách lưu vector:

- `ChromaKnowledgeVectorStore.UpsertChunksAsync(...)`.
- Payload gồm `ids`, `embeddings`, `documents`, `metadatas`.

Cách search vector:

- `ChromaKnowledgeVectorStore.QueryAsync(...)`.
- Include `documents`, `metadatas`, `distances`.
- Filter metadata được build trong `BuildWhere(...)`.

Cách chọn top chunks:

- Vector candidates: 50.
- Keyword candidates: 20.
- Merge theo chunk id.
- Permission/status recheck.
- Rerank thủ công.
- Pack tối đa 8 chunks, tối đa 3 chunks/source item.

Có hybrid search không?

- Có.
- Vector search + keyword search SQLite.
- Merge trong `AiQuestionService.MergeCandidateResults(...)`.

Có full-text search không?

- Có keyword search thủ công bằng SQLite table `knowledge_chunk_indexes`.
- Chưa thấy SQLite FTS virtual table hoặc external full-text engine như Elasticsearch/OpenSearch.

Có rerank không?

- Có rerank heuristic.
- Score gồm:
  - source priority: Correction 100, Wiki 45, Document 20.
  - keyword matches.
  - all keyword match.
  - phrase match.
  - scope match.
  - vector distance score.
- Không thấy cross-encoder reranker hoặc LLM reranker.

Có lọc theo metadata không?

- Có.
- `KnowledgeQueryFilter` gồm folder ids, document id, include company visible, source types, statuses.

Có lọc theo quyền user không?

- Có.
- Trước retrieval: folder ids từ `FolderPermissionService.GetVisibleFolderIdsAsync(...)`.
- Sau retrieval: recheck SQLite cho document/wiki/correction.

Có xử lý tài liệu cũ/mới không?

- Có ở mức version gating.
- Document chunks chỉ được dùng nếu `Document.CurrentVersionId == version.Id` và version `Indexed`.
- Chưa thấy xóa vector cũ khỏi Chroma ngay khi version mới index; hệ thống dựa vào recheck để loại bỏ.

Có xử lý tài liệu mâu thuẫn không?

- Có field `Conflicts` trong `AiAnswerDraft` và prompt yêu cầu trả conflicts.
- Chưa thấy module phát hiện mâu thuẫn tự động giữa tài liệu trước retrieval hoặc khi ingestion.
- Chưa thấy workflow riêng để reviewer resolve conflicts.

Đánh giá ngắn:

- Đây không còn là RAG cơ bản.
- Gần với `Knowledge Harness` hơn, vì có knowledge governance, permission recheck, feedback loop, correction, evaluation, retrieval explain và audit.
- Chưa phải `AI Agent Harness`, vì chưa có tool calling/hành động tự động do LLM quyết định.

## 6. Prompt và cách gọi LLM

Prompt trả lời Q&A:

- File: `Infrastructure/AiProvider/AnswerGenerationService.cs`.
- Service: `OpenAiCompatibleAnswerGenerationService.GenerateAsync(...)`.

Yêu cầu chính của system prompt:

- Là Internal Knowledge Copilot cho Vietnamese internal knowledge base.
- Trả lời tiếng Việt.
- Chỉ dùng provided sources.
- Không bịa facts, policies, prices, dates, procedures.
- Nếu nguồn không đủ hoặc mơ hồ, nói thiếu gì và hỏi lại ngắn gọn.
- Không nhắc nguồn không có trong prompt.
- Return JSON only với schema gồm answer, confidence, needsClarification, clarifyingQuestion, citations, missingInformation, conflicts, suggestedFollowUps.
- Chỉ cite sourceId được cung cấp.

Prompt wiki draft:

- File: `Infrastructure/AiProvider/WikiDraftGenerationService.cs`.
- Service: `OpenAiCompatibleWikiDraftGenerationService.GenerateAsync(...)`.
- Yêu cầu tạo wiki draft từ approved source document.
- Chỉ dùng source document content.
- Không bịa owners, dates, policies, prices, SLAs, approval rules.
- Return JSON schema rồi service convert sang Markdown.

Prompt document understanding:

- File: `Infrastructure/AiProvider/DocumentUnderstandingService.cs`.
- Service: `OpenAiCompatibleDocumentUnderstandingService.AnalyzeAsync(...)`.
- Yêu cầu phân tích title, outline, text.
- Return JSON gồm language, documentType, summary, keyTopics, entities, effectiveDate, sensitivity, qualityWarnings.
- Không bịa dates, owners, policies, prices, entities.

Provider call:

- `OpenAiCompatibleClient.CompleteAsync(...)`.
- Hỗ trợ:
  - OpenAI-compatible chat completions.
  - OpenAI responses API.
  - Anthropic messages.

Có bắt model chỉ trả lời dựa trên tài liệu không?

- Có, trong Q&A prompt và wiki/document understanding prompt.

Có yêu cầu citation/source không?

- Có, Q&A prompt yêu cầu `citations` với sourceId.
- Backend map `S1/S2/...` sang `RetrievedKnowledgeChunk.SourceId`.

Có xử lý trường hợp không tìm thấy tài liệu không?

- Có.
- Nếu không có chunks, mock và OpenAI-compatible service trả answer dạng chưa tìm thấy nguồn phù hợp, `NeedsClarification = true`, `Confidence = low`.

Có chống hallucination không?

- Có guardrails ở prompt và JSON parsing.
- Có kiểm tra nếu không có citations mà không needs clarification thì hạ confidence xuống low.
- Tuy nhiên chống hallucination vẫn phụ thuộc model và không có verifier độc lập.

Có phân biệt câu hỏi ngoài phạm vi không?

- Một phần.
- Nếu không có source phù hợp trong scope/permission, trả needs clarification/low confidence.
- Chưa thấy classifier riêng để phân loại câu hỏi ngoài phạm vi trước retrieval.

Có prompt cho từng use case khác nhau không?

- Có:
  - Q&A answer generation.
  - Wiki draft generation.
  - Document understanding.
  - AI provider settings health check.

Chưa thấy trong source code:

- Prompt registry/versioning.
- A/B prompt experiment.
- Lưu prompt cuối cùng vào DB.
- Prompt template file riêng ngoài code.

## 7. Citation, nguồn tham chiếu và độ tin cậy

Hiển thị nguồn tài liệu cho câu trả lời:

- Có.
- `AskQuestionResponse.Citations`.
- Frontend hiển thị ở `AiQuestionPage.vue`.

Hiển thị đoạn trích/chunk được dùng:

- Có excerpt.
- Backend tạo bằng `ToExcerpt(chunk.Text)`.
- Bảng `ai_interaction_sources` lưu `Excerpt`.

Hiển thị link file gốc:

- Chưa thấy trong citation response.
- Có API download document `GET /api/documents/{id}/download`, nhưng citation object không chứa URL/file link/document id để click mở nguồn từ answer.

Hiển thị điểm liên quan/score:

- Q&A response cho user không hiển thị score/distance.
- Reviewer có `RetrievalExplainPage.vue` và API `POST /api/ai/retrieval/explain` hiển thị score, distance, decision, matched keywords, reasons.

Cho phép người dùng mở tài liệu nguồn:

- Có download document ở document list/detail.
- Chưa thấy chức năng click citation để mở đúng tài liệu/chunk nguồn trong Q&A UI.

Câu trả lời có gắn citation không?

- Có.
- Citation gồm source type, title, folder path, section title, excerpt.

Có cảnh báo khi không đủ dữ liệu không?

- Có.
- `NeedsClarification`, `Confidence`, `MissingInformation`.
- Frontend hiển thị cảnh báo khi `answer.needsClarification`.

Rủi ro:

- Citation không chứa stable deep link tới document/wiki/correction.
- User thường chỉ thấy excerpt, không thấy toàn bộ chunk hoặc file gốc ngay từ citation.
- Nếu model trả answer có citation không đầy đủ, backend chỉ map valid source ids, chưa có groundedness verifier độc lập.

## 8. Logging, feedback và evaluation

Có lưu câu hỏi user không?

- Có.
- `AiInteractionEntity.Question`.

Có lưu câu trả lời AI không?

- Có.
- `AiInteractionEntity.Answer`.

Có lưu retrieved chunks không?

- Có một phần.
- Lưu cited chunks/sources vào `AiInteractionSourceEntity`: source type, source id, document id, version id, wiki page id, title, folder path, section title, excerpt, rank.
- Chưa lưu toàn bộ candidate set và rejected candidates của mỗi normal Q&A request.
- Retrieval explain có thể tính và trả candidates nhưng không thấy lưu kết quả explain vào DB.

Có lưu prompt cuối cùng gửi cho LLM không?

- Chưa thấy trong source code.

Có lưu model/provider được dùng không?

- AI provider settings có trong DB.
- Chưa thấy model/provider snapshot lưu trong từng `AiInteractionEntity` hoặc `EvaluationRunResultEntity`.

Có feedback đúng/sai từ user không?

- Có.
- API: `POST /api/ai/interactions/{id}/feedback`.
- Entity: `AiFeedbackEntity`.
- UI: `AiQuestionPage.vue`.

Có rating câu trả lời không?

- Có dạng nhị phân `Correct`/`Incorrect`.
- Chưa thấy rating dạng sao/1-5 hoặc score chi tiết từ user.

Có bộ câu hỏi test chuẩn không?

- Có evaluation cases.
- Tạo từ feedback sai qua `EvaluationService.CreateCaseFromFeedbackAsync(...)`.
- Entity: `EvaluationCaseEntity`.

Có evaluation tự động không?

- Có chạy evaluation qua API:

```text
POST /api/evaluation/runs
EvaluationService.RunAsync(...)
```

- Cách chấm hiện tại là keyword matching trong actual answer, không phải LLM judge.

Có so sánh chất lượng giữa các prompt/model không?

- Chưa thấy trong source code.
- Evaluation run có pass rate nhưng không lưu model/prompt version để so sánh rõ.

Có dashboard theo dõi chất lượng không?

- Có.
- `DashboardController.GetSummary(...)` trả AI question count, feedback correct/incorrect, pending incorrect, evaluation case count, latest evaluation pass rate, top cited sources.
- UI: `DashboardPage.vue`.

Đánh giá:

- Hệ thống đã có nền tảng đo chất lượng RAG ở mức pilot/early production: interaction log, citations, feedback, quality issue, correction, evaluation, dashboard.
- Thiếu để thành AI Harness tốt hơn:
  - prompt/model snapshot theo interaction.
  - full retrieval trace lưu DB.
  - raw LLM output/error classification chi tiết.
  - automatic groundedness/citation evaluator.
  - regression dataset độc lập, không chỉ tạo từ feedback.
  - so sánh model/prompt theo version.

## 9. Tool calling và agent behavior

AI có được gọi API nội bộ không?

- Chưa thấy trong source code.
- Backend service gọi API nội bộ/DB theo logic application, nhưng LLM không tự chọn tool/API.

AI có được đọc issue/ticket không?

- Chưa thấy trong source code.

AI có được đọc log không?

- Chưa thấy trong source code.
- Có audit log và quality issue, nhưng không có tool để LLM đọc log runtime.

AI có được truy vấn database không?

- Chưa thấy LLM có quyền truy vấn DB.
- Backend query DB deterministic trước/sau retrieval.

AI có được tạo ticket không?

- Chưa thấy trong source code.

AI có được gửi email/thông báo không?

- Chưa thấy trong source code.

AI có được tạo/sửa tài liệu không?

- Một phần, nhưng không theo agent behavior.
- AI sinh wiki draft qua `WikiDraftGenerationService`, reviewer quyết định publish.
- AI không tự sửa tài liệu nguồn.

AI có được chạy SQL/code không?

- Chưa thấy trong source code.

Có cơ chế human approval không?

- Có.
- Reviewer approve/reject document.
- Reviewer publish/reject wiki draft.
- Reviewer approve/reject knowledge correction.

Có giới hạn quyền của AI không?

- Có ở mức context được cấp cho LLM: backend chỉ đưa allowed chunks vào prompt.
- Không có autonomous tool permissions vì chưa có tool calling.

Có audit log cho hành động của AI không?

- Có audit cho workflow AI-adjacent như wiki draft generated, wiki published, feedback submitted/reviewed, correction created/approved/rejected, evaluation run, provider settings update.
- Chưa thấy audit cho từng external LLM call hoặc prompt invocation.

Kết luận:

- Đây là RAG app/AI assistant có governance và quality loop.
- Gần `Knowledge Harness`.
- Chưa phải `AI Agent Harness`, vì thiếu tool calling, planner, action execution, agent memory, approval gates cho tool actions và audit từng tool call.

## 10. Bảo mật và phân quyền

User login bằng gì:

- Email/password.
- Password hash qua `PasswordHasher`.
- JWT Bearer token qua `JwtTokenService`.

Role/permission hoạt động thế nào:

- Role claim trong JWT.
- `[Authorize]` và `[Authorize(Roles = ...)]` trên controller/action.
- Admin/Reviewer có quyền rộng hơn.
- User thường bị giới hạn theo folder permission.

Tài liệu có phân quyền không:

- Có theo folder.
- `FolderPermissionService` tính visible folders từ user permission và team permission.
- Admin/Reviewer thấy tất cả folder chưa deleted.
- Chưa thấy ACL riêng từng document.

Vector search có lọc theo quyền không:

- Có filter metadata trước query Chroma.
- Có recheck SQLite sau query.

Có nguy cơ user thấy tài liệu không được phép không?

- Rủi ro được giảm đáng kể do recheck SQLite trong `AnalyzeCandidateAccessAsync(...)`.
- Vẫn cần test kỹ vì vector metadata có thể sai, nhưng code không chỉ tin metadata.

Có kiểm soát dữ liệu gửi lên LLM provider không?

- Có giới hạn context theo chunks đã được permission filter.
- Có trim chunk text trong prompt `MaxChunkCharacters = 1600`.
- Wiki/document understanding cũng trim source text.
- Chưa thấy data loss prevention/masking/PII redaction trước khi gửi provider.

Có mask thông tin nhạy cảm không?

- Chưa thấy trong source code.
- Có detect `Sensitivity` khi document understanding, nhưng chưa thấy dùng để mask hoặc block gửi LLM.

Có audit log không?

- Có.
- `AuditLogService` và `AuditLogsController`.

Có rate limit không?

- Chưa thấy trong source code.
- Không thấy ASP.NET RateLimiter setup trong `Program.cs`.

Các rủi ro bảo mật chính:

- API key AI provider lưu trong SQLite qua `AiProviderSettingEntity.ApiKey`/`EmbeddingApiKey`; chưa thấy encryption-at-rest trong source code.
- Không thấy PII/sensitive data masking trước external LLM.
- Không thấy rate limit cho `/api/ai/ask`.
- Không thấy per-provider allowlist ngoài URL validation.

## 11. So sánh với AI Harness

| Thành phần AI Harness | Dự án hiện có chưa | Bằng chứng trong source code | Đánh giá |
|---|---|---|---|
| Knowledge base | Có | `DocumentsController`, `WikiService`, `KnowledgeChunkLedgerService`, `KnowledgeChunkEntity` | Có document/wiki/correction knowledge, versioning và ledger |
| Retrieval layer | Có | `AiQuestionService`, `ChromaKnowledgeVectorStore`, `KnowledgeKeywordIndexService` | Hybrid retrieval + permission recheck + rerank heuristic |
| Prompt orchestration | Một phần | `AnswerGenerationService`, `WikiDraftGenerationService`, `DocumentUnderstandingService` | Có prompt theo use case, nhưng chưa có prompt registry/versioning |
| Citation/grounding | Có | `AiCitationResponse`, `AiInteractionSourceEntity`, Q&A prompt | Có citation/excerpt; thiếu deep link/source preview từ citation |
| Logging/observability | Một phần | `AiInteractionEntity`, `AiInteractionSourceEntity`, `AuditLogService`, `DashboardController` | Tốt cho nghiệp vụ, thiếu prompt/model/raw response/retrieval trace đầy đủ |
| Evaluation | Một phần | `EvaluationService`, `EvaluationCaseEntity`, `EvaluationRunEntity` | Có regression keyword eval; chưa có LLM judge/groundedness eval/model compare |
| Permission/governance | Có | `FolderPermissionService`, `AnalyzeCandidateAccessAsync`, reviewer approve flows | Mạnh hơn RAG cơ bản; SQLite là source of truth |
| Tool calling | Không | Chưa thấy tool/function call schema hoặc LLM-selected tools | Không phải agent harness |
| Workflow automation | Một phần | `ProcessingJobWorker`, document processing jobs, classify failure jobs | Automation backend deterministic, không phải agent workflow |
| Human approval | Có | document approve/reject, wiki publish/reject, correction approve/reject | Đây là điểm rất giống harness/governance |
| Agent memory | Một phần | `AiInteractionEntity`, `AiFeedbackEntity`, `KnowledgeCorrectionEntity` | Có lịch sử/feedback/correction; chưa có conversational memory hoặc long-term agent memory |

## 12. Chấm mức độ trưởng thành

Thang đánh giá:

```text
Level 1: Chat với tài liệu đơn giản
Level 2: RAG cơ bản
Level 3: RAG production
Level 4: Knowledge Harness
Level 5: Workflow/Agent Harness
Level 6: AI Agent Platform nội bộ
```

Dự án hiện tại: **Level 4 - Knowledge Harness, mức đầu**.

Vì sao vượt Level 2:

- Có vector DB, embedding, chunking, Q&A.
- Có citations.
- Có document approval.
- Có permission filtering.

Vì sao đạt Level 3:

- Có hybrid search.
- Có rerank/packing.
- Có source priority.
- Có SQLite revalidation sau vector retrieval.
- Có logging interaction/source.
- Có feedback và dashboard.
- Có evaluation cơ bản.

Vì sao chạm Level 4:

- Có governance quanh knowledge: approve document, publish wiki, approve correction.
- Có quality issue loop từ incorrect feedback.
- Có correction được index như knowledge source ưu tiên.
- Có retrieval explain cho reviewer.
- Có rebuild index từ ledger.

Vì sao chưa đạt Level 5:

- Chưa có tool calling.
- Chưa có AI tự gọi internal APIs.
- Chưa có agent planner/executor.
- Chưa có human approval cho tool action do AI đề xuất.
- Chưa có workflow automation do LLM điều phối.

Còn thiếu để lên level tiếp theo:

- Tool registry với permission rõ ràng.
- Read-only tools trước: search documents, lookup interaction, lookup feedback, inspect audit/retrieval trace.
- Approval workflow cho write tools.
- Tool-call audit trail.
- Prompt/model versioning và evaluation theo version.
- Guardrails cho hành động nguy hiểm.

## 13. Điểm mạnh

- Kiến trúc module rõ: backend modules, infrastructure boundaries, frontend API wrappers.
- Có abstraction AI provider: `IEmbeddingService`, `IAnswerGenerationService`, `IDocumentUnderstandingService`, `IWikiDraftGenerationService`.
- Có Chroma vector DB sau interface `IKnowledgeVectorStore`.
- Có document upload/review/versioning.
- Có PDF/DOCX/Markdown/TXT extraction.
- Có chunking theo section với overlap.
- Có document understanding metadata.
- Có hybrid retrieval vector + keyword.
- Có rerank heuristic và source priority correction > wiki > document.
- Có permission filter bằng metadata và recheck SQLite.
- Có citations và lưu cited sources.
- Có feedback correct/incorrect.
- Có quality issue và correction workflow.
- Có human approval cho document/wiki/correction.
- Có evaluation case/run.
- Có dashboard KPI.
- Có audit log cho hành động nghiệp vụ quan trọng.
- Có retrieval explain UI/API cho reviewer.
- Có knowledge chunk ledger để rebuild/debug vector index.

## 14. Điểm thiếu/rủi ro

- Chưa thấy lưu prompt cuối cùng gửi LLM.
- Chưa thấy lưu raw LLM response theo interaction.
- Chưa thấy lưu model/provider snapshot theo từng Q&A interaction.
- Chưa thấy score threshold cứng cho vector distance.
- Chưa thấy LLM/cross-encoder reranker.
- Chưa thấy groundedness verifier độc lập sau khi model trả lời.
- Chưa thấy deep link citation tới file/chunk nguồn.
- Chưa thấy source preview từ citation trong Q&A UI.
- Chưa thấy PII/sensitive masking trước khi gửi LLM provider.
- Chưa thấy encryption-at-rest cho AI API key trong SQLite.
- Chưa thấy rate limit cho AI ask endpoint.
- Chưa thấy tool calling/function calling.
- Chưa thấy issue/ticket/email/log/database connector cho agent.
- Chưa thấy agent memory/conversation thread memory.
- Chưa thấy conflict detection tự động giữa nhiều tài liệu.
- Chưa thấy prompt/model A/B comparison.
- Chưa thấy evaluation bằng LLM judge hoặc citation-groundedness metric.
- Chưa thấy xóa vector cũ khỏi Chroma khi document version thay đổi; hiện dựa vào SQLite revalidation để loại bỏ.

## 15. Đề xuất roadmap nâng cấp

### Giai đoạn 1: Làm RAG đáng tin hơn

- Lưu prompt cuối cùng, model/provider, temperature, max tokens, context source ids theo từng `AiInteraction`.
- Lưu toàn bộ retrieval trace: vector candidates, keyword candidates, rejected candidates, final context, score reasons.
- Thêm score/distance threshold cấu hình được.
- Thêm source deep link trong citation: document id, version id, wiki page id, source id, section id/chunk id.
- Thêm source preview từ citation trong UI.
- Thêm groundedness check cơ bản: answer sentence phải map được tới citation hoặc giảm confidence.
- Thêm PII/sensitive data redaction trước khi gửi LLM.
- Thêm rate limit cho `/api/ai/ask`.

### Giai đoạn 2: Lên Knowledge Harness

- Prompt registry/versioning trong DB.
- Lưu model/prompt version vào evaluation run và AI interaction.
- Mở rộng evaluation set độc lập, không chỉ từ feedback.
- Thêm LLM judge hoặc rule-based citation coverage metric.
- Thêm reranker chuyên biệt hoặc LLM rerank có kiểm soát.
- Thêm conflict detection giữa current document/wiki/correction.
- Thêm lifecycle cho stale vectors: delete/deactivate/rebuild theo source.
- Dùng sensitivity metadata để enforce policy, ví dụ block external provider với confidential docs.
- Mở rộng dashboard chất lượng: pass rate theo model/prompt, incorrect by source, retrieval miss rate.

### Giai đoạn 3: Lên AI Agent Harness

- Thiết kế tool registry:
  - read-only tools trước.
  - write tools sau approval.
- Tool read-only đề xuất:
  - search knowledge.
  - explain retrieval.
  - get document metadata.
  - get wiki page.
  - get feedback/quality issue.
  - get audit log theo entity.
- Tool write có human approval:
  - create correction draft.
  - propose wiki draft.
  - propose evaluation case.
  - request reindex.
- Thêm agent action plan và approval UI.
- Thêm audit log cho từng tool call: input, output summary, actor, approval, result.
- Thêm policy engine cho tool permissions theo role/user/scope.
- Thêm guardrails cho hành động nguy hiểm: không chạy SQL/code, không gửi external notification nếu chưa approve.
- Chỉ sau đó mới cân nhắc connectors như issue/ticket/log/database lookup/email.

## 16. Kết luận ngắn gọn

Dự án này có giống AI Harness, nhưng hiện giống nhất ở lớp **Knowledge Harness** chứ chưa phải **AI Agent Harness**. Hệ thống đã có RAG khá trưởng thành: document approval, chunking, embedding, Chroma, hybrid retrieval, permission recheck, citations, feedback, correction, evaluation, dashboard và audit. Thành phần giống harness nhất là governance quanh tri thức: chỉ dùng document indexed/current, wiki phải publish, correction phải approve, retrieval phải qua permission filter. Thành phần thiếu nhất là agent/tool layer: LLM chưa được gọi tool, chưa có planner/executor, chưa có action approval cho tool calls. Nếu muốn nâng cấp, nên ưu tiên logging/prompt/model trace, retrieval trace, citation deep link, groundedness evaluation và sensitivity guardrails trước; sau đó mới thêm tool calling và workflow automation.
