# Giả lập một đoạn text đi qua code cho đến khi thành tài liệu tri thức

Tài liệu này mô phỏng một file text đơn giản được upload vào Internal Knowledge Copilot và mô tả nó đi qua các luồng, class và hàm nào trong code cho đến khi trở thành nguồn tri thức có thể dùng cho AI Q&A.

Tài liệu tham chiếu nền: `docs/technical/DOCUMENT_UPLOAD_TO_KNOWLEDGE_FLOW.md`.

## 1. Thông tin text giả lập

Giả sử user upload file `quy-trinh-xin-nghi-phep.md` vào folder `Nhân sự / Chính sách`.

Nội dung file:

```markdown
# Quy trình xin nghỉ phép

## Mục đích
Nhân viên cần gửi đơn xin nghỉ phép trước ít nhất 3 ngày làm việc, trừ trường hợp khẩn cấp.

## Phạm vi
Áp dụng cho toàn bộ nhân viên chính thức và thử việc.

## Các bước
1. Nhân viên tạo yêu cầu nghỉ phép trên hệ thống HRM.
2. Quản lý trực tiếp phê duyệt hoặc từ chối yêu cầu.
3. Bộ phận Nhân sự kiểm tra số ngày phép còn lại.
4. Nếu hợp lệ, yêu cầu được ghi nhận vào bảng công.

## Lưu ý
Nghỉ phép từ 5 ngày liên tiếp trở lên cần thông báo trước ít nhất 10 ngày làm việc.
```

Metadata upload giả lập:

```text
folderId = <folder-nhan-su-chinh-sach>
title = "Quy trình xin nghỉ phép"
description = "Hướng dẫn nội bộ về xin nghỉ phép"
file = quy-trinh-xin-nghi-phep.md
```

## 2. Pha upload: text trở thành Document và DocumentVersion

Frontend gọi:

```http
POST /api/documents
Content-Type: multipart/form-data
```

Backend nhận request tại:

```text
src/backend/InternalKnowledgeCopilot.Api/Modules/Documents/DocumentsController.cs
DocumentsController.Upload(...)
```

Các bước chính trong code:

1. `DocumentsController.Upload` lấy user id bằng `GetCurrentUserId()`.
2. `fileUploadValidator.Validate(request.File)` gọi `FileUploadValidator.Validate(...)`.
3. `folderPermissionService.CanViewFolderAsync(userId, request.FolderId, ...)` kiểm tra user có quyền với folder.
4. `request.Title.Trim()` được validate để tránh title rỗng.
5. Backend tạo `DocumentEntity` với `Status = PendingReview`.
6. Backend gọi `CreateVersionAsync(document.Id, 1, request.File!, ...)`.
7. `CreateVersionAsync` gọi `fileStorageService.SaveDocumentVersionAsync(...)`.
8. `FileStorageService.SaveDocumentVersionAsync(...)` lưu file gốc vào storage.
9. `CreateVersionAsync` tạo `DocumentVersionEntity` với `Status = PendingReview`.
10. `dbContext.Documents.Add(document)` và `dbContext.DocumentVersions.Add(version)` ghi metadata vào SQLite.
11. `auditLogService.RecordAsync(..., "DocumentUploaded", ...)` ghi audit.

Sau bước này, đoạn text giả lập chưa phải tri thức. Nó mới là file gốc đã lưu trong local storage và metadata trong SQLite.

Trạng thái dữ liệu:

```text
Document.Status = PendingReview
Document.CurrentVersionId = null

DocumentVersion.VersionNumber = 1
DocumentVersion.Status = PendingReview
DocumentVersion.OriginalFileName = quy-trinh-xin-nghi-phep.md
DocumentVersion.StoredFilePath = <storage>/documents/<documentId>/<versionId>/<safe-file-name>.md
```

## 3. Pha reviewer approve: tạo processing job

Reviewer/Admin gọi:

```http
POST /api/documents/{documentId}/approve
```

Backend xử lý tại:

```text
DocumentsController.Approve(...)
```

Các bước chính:

1. `Approve` lấy reviewer id bằng `GetCurrentUserId()`.
2. Load `DocumentEntity` cùng `Versions`.
3. Tìm `DocumentVersionEntity` theo `request.VersionId`.
4. Set version:

```text
version.Status = Approved
version.ReviewedByUserId = reviewerId
version.ReviewedAt = now
```

5. Set document:

```text
document.CurrentVersionId = version.Id
document.Status = Approved
```

6. Tạo `ProcessingJobEntity`:

```text
JobType = "ExtractAndEmbedDocument"
TargetType = "DocumentVersion"
TargetId = version.Id
Status = Pending
Attempts = 0
```

7. `auditLogService.RecordAsync(..., "DocumentApproved", ...)` ghi audit.

Sau bước này, document đã được duyệt về mặt nghiệp vụ, nhưng vẫn chưa dùng được cho RAG nếu version chưa `Indexed`.

## 4. Background worker lấy job xử lý

Hosted service chạy tại:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/BackgroundJobs/ProcessingJobWorker.cs
ProcessingJobWorker.ExecuteAsync(...)
ProcessingJobWorker.ProcessNextJobAsync(...)
```

Luồng trong `ProcessNextJobAsync`:

1. Query SQLite tìm job:

```text
Status == Pending
Attempts < maxAttempts
```

2. Set job:

```text
job.Status = Running
job.Attempts += 1
job.StartedAt = now
```

3. Vì job có:

```text
JobType = "ExtractAndEmbedDocument"
TargetType = "DocumentVersion"
```

worker gọi:

```text
processingService.ProcessDocumentVersionAsync(job.TargetId, ...)
```

Service thật là:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/DocumentProcessing/DocumentProcessingService.cs
DocumentProcessingService.ProcessDocumentVersionAsync(...)
```

Nếu xử lý thành công, worker set:

```text
job.Status = Succeeded
job.FinishedAt = now
```

Nếu lỗi quá số lần retry, worker set:

```text
job.Status = Failed
version.Status = ProcessingFailed
```

## 5. Processing: file text trở thành chunks có embedding

Toàn bộ pha này nằm trong:

```text
DocumentProcessingService.ProcessDocumentVersionAsync(documentVersionId, ...)
```

### 5.1 Load version, document và folder

Service load:

```text
dbContext.DocumentVersions
  .Include(version => version.Document)
  .ThenInclude(document => document.Folder)
```

Sau đó set:

```text
version.Status = Processing
```

### 5.2 Extract text

Service gọi:

```text
textExtractor.ExtractAsync(version.StoredFilePath, version.FileExtension, ...)
```

Implementation:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/DocumentProcessing/DocumentTextExtractor.cs
DocumentTextExtractor.ExtractAsync(...)
```

Với file `.md`, `ExtractAsync` đi vào nhánh:

```text
".txt" or ".md" or ".markdown" => File.ReadAllTextAsync(path, ...)
```

Kết quả extract chính là nội dung Markdown giả lập ở mục 1.

Nếu text rỗng, `DocumentProcessingService` throw:

```text
Document has no extractable text.
```

### 5.3 Lưu extracted.txt

Service ghi raw text ra:

```text
<storage>/documents/<documentId>/<versionId>/extracted.txt
```

Sau xử lý thành công, path này được lưu vào:

```text
version.ExtractedTextPath
```

### 5.4 Normalize text

Service gọi:

```text
textNormalizer.Normalize(extractedText)
```

Implementation:

```text
DocumentTextNormalizer.Normalize(...)
```

Normalizer thực hiện:

1. Normalize Unicode về `NormalizationForm.FormC`.
2. Chuẩn hóa newline về `\n`.
3. Cảnh báo nếu có ký tự thay thế `\uFFFD`.
4. Cảnh báo nếu text có dấu hiệu mojibake như `Ã`, `Â`, `áº`, `á»`.
5. Xóa control characters không cần thiết.
6. Trim cuối dòng.
7. Collapse nhiều khoảng trắng/tab trong cùng dòng thành một khoảng trắng.
8. Giữ tối đa một dòng trống liên tiếp.

Kết quả được ghi ra:

```text
<storage>/documents/<documentId>/<versionId>/normalized.txt
```

Path được lưu vào:

```text
version.NormalizedTextPath
```

Service cũng tính:

```text
textHash = SHA256(normalized.Text)
```

và lưu vào:

```text
version.TextHash
```

### 5.5 Detect section

Service gọi:

```text
sectionDetector.Detect(normalized.Text)
```

Implementation:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/DocumentProcessing/SectionDetector.cs
SectionDetector.Detect(...)
```

Với text giả lập, detector nhận ra Markdown headings:

```text
# Quy trình xin nghỉ phép
## Mục đích
## Phạm vi
## Các bước
## Lưu ý
```

Các heading được biến thành danh sách `DocumentSection`:

```text
DocumentSection(Index, Title, StartOffset, EndOffset, Text)
```

Nếu không tìm thấy heading, detector fallback thành một section:

```text
Title = "Document"
```

### 5.6 AI phân tích document understanding

Service gọi:

```text
documentUnderstandingService.AnalyzeAsync(
  version.Document.Title,
  normalized.Text,
  sections,
  ...
)
```

Runtime service có thể route sang mock hoặc OpenAI-compatible provider:

```text
RuntimeDocumentUnderstandingService.AnalyzeAsync(...)
MockDocumentUnderstandingService.AnalyzeAsync(...)
OpenAiCompatibleDocumentUnderstandingService.AnalyzeAsync(...)
```

Kết quả được dùng để enrich metadata:

```text
version.DocumentSummary
version.Language
version.DocumentType
version.KeyTopicsJson
version.EntitiesJson
version.EffectiveDate
version.Sensitivity
version.QualityWarningsJson
```

Ví dụ suy luận hợp lý cho đoạn text giả lập:

```text
Language = "vi"
DocumentType = "procedure"
KeyTopics = ["nghỉ phép", "phê duyệt", "HRM", "bảng công"]
Entities = ["Nhân sự", "Quản lý trực tiếp"]
Sensitivity = "internal"
```

### 5.7 Chunk text

Service gọi:

```text
chunker.Chunk(normalized.Text, sections)
```

Implementation:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/DocumentProcessing/TextChunker.cs
TextChunker.Chunk(...)
```

Vì đã có sections, `Chunk` gọi tiếp:

```text
ChunkSections(sections)
ChunkText(section.Text, section.StartOffset, section.Title, section.Index, chunks.Count)
```

Quy tắc chính:

```text
TargetCharacters = 2800
OverlapCharacters = 350
```

Với đoạn text giả lập ngắn, mỗi section thường thành một chunk. Ví dụ:

```text
chunk 0: section "Quy trình xin nghỉ phép"
chunk 1: section "Mục đích"
chunk 2: section "Phạm vi"
chunk 3: section "Các bước"
chunk 4: section "Lưu ý"
```

Mỗi chunk là:

```text
TextChunk(Index, Text, SectionTitle, SectionIndex, StartOffset, EndOffset)
```

### 5.8 Tạo embedding và KnowledgeChunkRecord

Với từng `TextChunk`, service tạo id:

```text
chunkId = $"{version.Id:N}-{chunk.Index}"
```

Sau đó gọi:

```text
embeddingService.CreateEmbeddingAsync(chunk.Text, ...)
```

Runtime embedding có thể route sang:

```text
RuntimeEmbeddingService.CreateEmbeddingAsync(...)
MockEmbeddingService.CreateEmbeddingAsync(...)
OpenAiCompatibleEmbeddingService.CreateEmbeddingAsync(...)
OpenAiCompatibleClient.CreateEmbeddingAsync(...)
```

Mỗi chunk được đóng gói thành `KnowledgeChunkRecord`:

```text
KnowledgeChunkRecord(
  Id = chunkId,
  Embedding = <float[]>,
  Text = chunk.Text,
  Metadata = {...}
)
```

Metadata document chunk gồm các field quan trọng:

```json
{
  "source_type": "document",
  "source_id": "<documentVersionId>",
  "document_id": "<documentId>",
  "document_version_id": "<documentVersionId>",
  "folder_id": "<folderId>",
  "title": "Quy trình xin nghỉ phép",
  "folder_path": "Nhân sự / Chính sách",
  "version_number": 1,
  "status": "approved",
  "visibility_scope": "folder",
  "chunk_index": 3,
  "section_title": "Các bước",
  "section_index": 3,
  "char_start": 180,
  "char_end": 430,
  "language": "vi",
  "document_type": "procedure",
  "keywords": "nghỉ phép, phê duyệt, HRM, bảng công",
  "sensitivity": "internal",
  "text_hash": "<sha256>",
  "created_at": "<utc>"
}
```

### 5.9 Upsert vào Chroma vector DB

Service gọi:

```text
vectorStore.UpsertChunksAsync(vectorChunks, ...)
```

Implementation:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/VectorStore/ChromaKnowledgeVectorStore.cs
ChromaKnowledgeVectorStore.UpsertChunksAsync(...)
```

Từ đây, mỗi chunk có vector embedding trong Chroma collection. Vector DB phục vụ semantic search, nhưng quyền truy cập vẫn phải dựa vào SQLite và metadata filter.

### 5.10 Ghi ledger vào SQLite

Service gọi:

```text
chunkLedgerService.ReplaceChunksAsync(
  KnowledgeSourceType.Document,
  version.Id.ToString(),
  vectorChunks,
  ...
)
```

Implementation:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/KnowledgeIndex/KnowledgeChunkLedgerService.cs
KnowledgeChunkLedgerService.ReplaceChunksAsync(...)
```

Ledger là bản ghi SQLite của chunks, giúp hệ thống có thể kiểm toán, rebuild index hoặc đọc snapshot theo source.

### 5.11 Ghi keyword index vào SQLite

Service gọi:

```text
keywordIndexService.ReplaceChunksAsync(
  KnowledgeSourceType.Document,
  version.Id.ToString(),
  vectorChunks,
  ...
)
```

Implementation:

```text
src/backend/InternalKnowledgeCopilot.Api/Infrastructure/KeywordSearch/KnowledgeKeywordIndexService.cs
KnowledgeKeywordIndexService.ReplaceChunksAsync(...)
```

Keyword index phục vụ lexical search, bổ sung cho vector search khi user hỏi Q&A.

### 5.12 Kết thúc processing

Cuối `ProcessDocumentVersionAsync`, service set:

```text
version.ExtractedTextPath = extractedTextPath
version.NormalizedTextPath = normalizedTextPath
version.SectionCount = sections.Count
version.ProcessingWarningsJson = normalized.WarningsJson
version.TextHash = textHash
version.Status = Indexed
version.IndexedAt = now
version.UpdatedAt = now
```

Từ thời điểm này, đoạn text giả lập đã trở thành document knowledge:

```text
SQLite:
  Documents
  DocumentVersions
  KnowledgeChunks
  KnowledgeChunkIndexes

Local storage:
  original .md
  extracted.txt
  normalized.txt

Chroma:
  vector chunks source_type=document
```

## 6. Khi nào document này được coi là tri thức?

Document giả lập được coi là nguồn tri thức cho AI Q&A khi thỏa các điều kiện:

```text
Document.DeletedAt == null
Document.Status == Approved
Document.CurrentVersionId == version.Id
DocumentVersion.Status == Indexed
Chunks đã upsert vào Chroma
Chunks đã ghi vào KnowledgeChunkLedgerService
Chunks đã ghi vào KnowledgeKeywordIndexService
User hỏi có quyền với folder chứa document
```

Nếu chỉ dừng ở `Approved` nhưng chưa `Indexed`, document chưa thật sự sẵn sàng cho RAG.

## 7. Pha generate wiki draft: document knowledge thành bản nháp chuẩn hóa

Reviewer/Admin gọi:

```http
POST /api/wiki/generate
```

Request:

```json
{
  "documentId": "<documentId>",
  "documentVersionId": "<documentVersionId>"
}
```

Backend xử lý tại:

```text
src/backend/InternalKnowledgeCopilot.Api/Modules/Wiki/WikiService.cs
WikiService.GenerateDraftAsync(...)
```

Các bước chính:

1. Load `DocumentVersion` cùng `Document` và `Folder`.
2. Kiểm tra:

```text
version.Status == Indexed
version.Document.CurrentVersionId == version.Id
document.DeletedAt == null
```

3. Kiểm tra quyền folder:

```text
folderPermissionService.CanViewFolderAsync(reviewerId, version.Document.FolderId, ...)
```

4. Chọn source text:

```text
ưu tiên version.NormalizedTextPath
fallback version.ExtractedTextPath
```

5. Đọc source text bằng `File.ReadAllTextAsync(...)`.
6. Tìm tài liệu liên quan bằng:

```text
FindRelatedDocumentsAsync(...)
```

7. Sinh wiki draft bằng:

```text
draftGenerationService.GenerateAsync(version.Document.Title, sourceText, ...)
```

Runtime draft generation có thể route sang:

```text
RuntimeWikiDraftGenerationService.GenerateAsync(...)
MockWikiDraftGenerationService.GenerateAsync(...)
OpenAiCompatibleWikiDraftGenerationService.GenerateAsync(...)
```

8. Append section `## Related documents` bằng:

```text
AppendRelatedDocumentsSection(...)
```

9. Tạo `WikiDraftEntity`:

```text
Status = Draft
SourceDocumentId = documentId
SourceDocumentVersionId = versionId
Title = "Quy trình xin nghỉ phép"
Content = <markdown AI sinh>
MissingInformationJson = ...
RelatedDocumentsJson = ...
```

10. Ghi audit:

```text
WikiDraftGenerated
```

Ở bước này, wiki draft chưa phải tri thức chính thức. Nó cần reviewer publish.

## 8. Pha publish wiki: wiki draft thành wiki knowledge

Reviewer/Admin gọi:

```http
POST /api/wiki/drafts/{draftId}/publish
```

Backend xử lý tại:

```text
WikiService.PublishAsync(...)
```

Các bước chính:

1. Load `WikiDraftEntity` và source document.
2. Kiểm tra `draft.Status == Draft`.
3. Nếu publish scope là company, kiểm tra `request.IsCompanyPublicConfirmed`.
4. Nếu publish theo folder, kiểm tra quyền bằng `folderPermissionService.CanViewFolderAsync(...)`.
5. Tạo `WikiPageEntity`:

```text
SourceDraftId = draft.Id
SourceDocumentId = draft.SourceDocumentId
SourceDocumentVersionId = draft.SourceDocumentVersionId
Title = draft.Title
Content = draft.Content
VisibilityScope = Folder hoặc Company
PublishedAt = now
```

6. Set:

```text
draft.Status = Published
draft.ReviewedByUserId = reviewerId
draft.ReviewedAt = now
```

7. Gọi:

```text
IndexWikiPageAsync(page, folderPath, draft, ...)
```

## 9. IndexWikiPageAsync: wiki page được chunk và embed lại

Hàm:

```text
WikiService.IndexWikiPageAsync(...)
```

Luồng xử lý:

1. Detect section wiki:

```text
sectionDetector.Detect(page.Content)
```

2. Chunk wiki markdown:

```text
chunker.Chunk(page.Content, sections)
```

3. Với từng wiki chunk, tạo embedding:

```text
embeddingService.CreateEmbeddingAsync(chunk.Text, ...)
```

4. Tạo `KnowledgeChunkRecord` với metadata:

```json
{
  "source_type": "wiki",
  "source_id": "<wikiPageId>",
  "wiki_page_id": "<wikiPageId>",
  "document_id": "<sourceDocumentId>",
  "document_version_id": "<sourceDocumentVersionId>",
  "folder_id": "<folderId hoặc rỗng nếu company>",
  "title": "Quy trình xin nghỉ phép",
  "status": "published",
  "visibility_scope": "folder hoặc company",
  "chunk_index": 0,
  "section_title": "Mục đích",
  "related_document_count": 0,
  "missing_information_count": 0
}
```

5. Upsert vào Chroma:

```text
vectorStore.UpsertChunksAsync(vectorChunks, ...)
```

6. Ghi ledger:

```text
chunkLedgerService.ReplaceChunksAsync(KnowledgeSourceType.Wiki, page.Id.ToString(), vectorChunks, ...)
```

7. Ghi keyword index:

```text
keywordIndexService.ReplaceChunksAsync(KnowledgeSourceType.Wiki, page.Id.ToString(), vectorChunks, ...)
```

8. Ghi audit:

```text
WikiPublished
```

Sau bước này, đoạn text ban đầu đã có thêm lớp wiki knowledge chuẩn hóa:

```text
SQLite:
  WikiDrafts.Status = Published
  WikiPages
  KnowledgeChunks source_type=Wiki
  KnowledgeChunkIndexes source_type=Wiki

Chroma:
  vector chunks source_type=wiki
  status=published
```

## 10. Sequence tổng hợp cho đoạn text giả lập

```text
User upload quy-trinh-xin-nghi-phep.md
  -> DocumentsController.Upload
  -> FileUploadValidator.Validate
  -> IFolderPermissionService.CanViewFolderAsync
  -> DocumentsController.CreateVersionAsync
  -> FileStorageService.SaveDocumentVersionAsync
  -> SQLite Documents + DocumentVersions
  -> Audit DocumentUploaded

Reviewer approve
  -> DocumentsController.Approve
  -> DocumentVersion.Status = Approved
  -> Document.Status = Approved
  -> ProcessingJobs.Add(JobType=ExtractAndEmbedDocument)
  -> Audit DocumentApproved

Background worker
  -> ProcessingJobWorker.ExecuteAsync
  -> ProcessingJobWorker.ProcessNextJobAsync
  -> IDocumentProcessingService.ProcessDocumentVersionAsync

Document processing
  -> DocumentProcessingService.ProcessDocumentVersionAsync
  -> DocumentTextExtractor.ExtractAsync
  -> write extracted.txt
  -> DocumentTextNormalizer.Normalize
  -> write normalized.txt
  -> SectionDetector.Detect
  -> IDocumentUnderstandingService.AnalyzeAsync
  -> TextChunker.Chunk
  -> IEmbeddingService.CreateEmbeddingAsync per chunk
  -> ChromaKnowledgeVectorStore.UpsertChunksAsync
  -> KnowledgeChunkLedgerService.ReplaceChunksAsync(Document)
  -> KnowledgeKeywordIndexService.ReplaceChunksAsync(Document)
  -> DocumentVersion.Status = Indexed
  -> ProcessingJob.Status = Succeeded

Reviewer generate wiki
  -> WikiService.GenerateDraftAsync
  -> read normalized.txt
  -> WikiService.FindRelatedDocumentsAsync
  -> IWikiDraftGenerationService.GenerateAsync
  -> SQLite WikiDrafts.Status = Draft
  -> Audit WikiDraftGenerated

Reviewer publish wiki
  -> WikiService.PublishAsync
  -> SQLite WikiPages
  -> WikiDraft.Status = Published
  -> WikiService.IndexWikiPageAsync
  -> SectionDetector.Detect
  -> TextChunker.Chunk
  -> IEmbeddingService.CreateEmbeddingAsync per wiki chunk
  -> ChromaKnowledgeVectorStore.UpsertChunksAsync
  -> KnowledgeChunkLedgerService.ReplaceChunksAsync(Wiki)
  -> KnowledgeKeywordIndexService.ReplaceChunksAsync(Wiki)
  -> Audit WikiPublished
```

## 11. Kết luận

Với file text giả lập `quy-trinh-xin-nghi-phep.md`, hệ thống không dùng nội dung upload ngay lập tức. Nội dung phải đi qua upload, lưu file, lưu metadata, reviewer approve, background job, extract, normalize, section detection, document understanding, chunking, embedding, vector upsert, ledger, keyword index và chuyển version sang `Indexed`.

Khi reviewer sinh và publish wiki, cùng nội dung đó đi thêm một vòng chuẩn hóa qua `WikiService.GenerateDraftAsync`, `WikiService.PublishAsync` và `IndexWikiPageAsync`. Lúc này hệ thống có hai lớp tri thức: document knowledge làm nguồn gốc/fallback và wiki knowledge làm nguồn ưu tiên cho AI Q&A.
