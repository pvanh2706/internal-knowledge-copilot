# Luồng upload tài liệu đến khi thành tri thức

Tài liệu này mô tả chi tiết luồng hiện tại của hệ thống Internal Knowledge Copilot: từ khi người dùng upload tài liệu, tài liệu được lưu vào cơ sở dữ liệu, được reviewer duyệt, được xử lý thành chunks, lưu vào vector DB, dùng cho AI Q&A, rồi được tạo wiki draft và publish thành tri thức chuẩn hóa.

## 1. Tổng quan ngắn

Luồng hiện tại không biến file upload thành tri thức ngay lập tức. Hệ thống tách thành nhiều pha:

1. User upload tài liệu.
2. Backend validate file và quyền truy cập folder.
3. File gốc được lưu vào local storage.
4. Metadata tài liệu và phiên bản tài liệu được lưu vào SQLite.
5. Tài liệu chờ Reviewer/Admin duyệt.
6. Reviewer approve phiên bản tài liệu.
7. Backend tạo processing job để xử lý bất đồng bộ.
8. Background worker lấy job và xử lý tài liệu.
9. Hệ thống extract text, normalize text, detect sections, phân tích hiểu tài liệu.
10. Hệ thống chunk nội dung.
11. Hệ thống tạo embedding cho từng chunk.
12. Chunks được lưu vào Chroma vector DB.
13. Chunks cũng được lưu vào SQLite ledger và keyword index.
14. Document version chuyển sang trạng thái Indexed.
15. Từ lúc này, tài liệu đã có thể dùng làm nguồn tri thức cho AI Q&A.
16. Reviewer có thể chủ động generate wiki draft từ document version đã Indexed.
17. Reviewer publish wiki draft.
18. Wiki page được lưu vào SQLite, chunk, embedding và lưu vào vector DB.
19. Wiki published trở thành nguồn tri thức ưu tiên hơn document gốc khi AI Q&A.

## 2. Các thành phần chính tham gia

### Frontend

Frontend Vue gọi API backend để:

- Upload tài liệu.
- Xem danh sách tài liệu.
- Xem chi tiết document và các version.
- Reviewer approve/reject document.
- Reviewer generate wiki draft.
- Reviewer publish/reject wiki draft.

Các API phía frontend nằm trong `src/frontend/src/api/documents.ts`, `src/frontend/src/api/wiki.ts`.

### Backend API

Backend ASP.NET Core chịu trách nhiệm chính:

- Nhận upload.
- Validate file.
- Kiểm tra quyền folder.
- Lưu metadata vào SQLite.
- Lưu file vào local filesystem.
- Tạo processing job.
- Xử lý document.
- Gọi AI provider.
- Gọi vector DB.
- Ghi audit log.

Các module quan trọng:

- `Modules/Documents/DocumentsController.cs`
- `Infrastructure/FileStorage/FileUploadValidator.cs`
- `Infrastructure/FileStorage/FileStorageService.cs`
- `Infrastructure/BackgroundJobs/ProcessingJobWorker.cs`
- `Infrastructure/DocumentProcessing/DocumentProcessingService.cs`
- `Infrastructure/VectorStore/ChromaKnowledgeVectorStore.cs`
- `Modules/Wiki/WikiService.cs`

### SQLite

SQLite là nguồn sự thật cho dữ liệu nghiệp vụ:

- Document.
- Document version.
- Processing job.
- Wiki draft.
- Wiki page.
- Audit log.
- Knowledge chunk ledger.
- Keyword index.
- Permission/folder/user/team.

### Local file storage

Local storage lưu:

- File gốc người dùng upload.
- File `extracted.txt`.
- File `normalized.txt`.

Đường dẫn file được lưu trong SQLite để backend có thể truy xuất lại.

### Vector DB

Runtime hiện tại dùng ChromaDB thông qua adapter `IKnowledgeVectorStore`.

Vector DB lưu:

- Vector embedding của document chunks.
- Vector embedding của wiki chunks.
- Metadata để filter theo source type, folder, document, visibility, status.

Vector DB không phải nguồn sự thật về quyền. Quyền vẫn dựa trên SQLite.

### AI provider

AI provider được dùng cho:

- Tạo embedding.
- Phân tích hiểu tài liệu.
- Sinh wiki draft.
- Sinh câu trả lời Q&A.

Hệ thống có mock service để chạy local/test và OpenAI-compatible service cho cấu hình thật.

## 3. Trạng thái dữ liệu chính

### DocumentStatus

Document có các trạng thái:

- `PendingReview`: tài liệu mới upload hoặc chưa có version current được duyệt.
- `Approved`: tài liệu đã có version được duyệt làm current version.
- `Rejected`: tài liệu bị reject và chưa có current version.
- `Archived`: tài liệu đã lưu trữ.
- `Deleted`: tài liệu đã xóa mềm.

### DocumentVersionStatus

Mỗi document có thể có nhiều version. Version có trạng thái riêng:

- `PendingReview`: version mới upload, chờ duyệt.
- `Approved`: reviewer đã duyệt, nhưng có thể chưa xử lý xong.
- `Processing`: worker đang extract/chunk/embed.
- `Indexed`: version đã xử lý xong và đã sẵn sàng cho tri thức.
- `Rejected`: version bị reviewer reject.
- `ProcessingFailed`: worker xử lý thất bại quá số lần retry.

### ProcessingJobStatus

Processing job có trạng thái:

- `Pending`: đang chờ worker xử lý.
- `Running`: worker đang xử lý.
- `Succeeded`: xử lý thành công.
- `Failed`: xử lý thất bại hết số lần thử.

### WikiStatus

Wiki draft có trạng thái:

- `Draft`: vừa được AI sinh ra, chờ reviewer kiểm tra.
- `Published`: reviewer đã publish thành wiki page.
- `Rejected`: reviewer reject draft.
- `Archived`: đã lưu trữ.

## 4. Pha upload tài liệu

### 4.1 User gửi request upload

User gửi request đến:

```http
POST /api/documents
```

Dữ liệu gửi lên là `multipart/form-data`, gồm:

- `folderId`: folder chứa tài liệu.
- `title`: tên tài liệu trong hệ thống.
- `description`: mô tả tùy chọn.
- `file`: file gốc.

Backend xử lý trong `DocumentsController.Upload`.

### 4.2 Backend xác thực user

Backend lấy user id từ JWT claim:

```text
ClaimTypes.NameIdentifier -> userId
```

Nếu token không hợp lệ hoặc không lấy được user id, API trả `401 Unauthorized`.

### 4.3 Backend validate file

Backend gọi `IFileUploadValidator.Validate`.

Các kiểm tra hiện tại:

- File bắt buộc tồn tại.
- File không được rỗng.
- File không vượt quá giới hạn upload, mặc định thông báo là 20MB.
- Extension phải thuộc danh sách cho phép.

Các định dạng được hỗ trợ trong MVP:

- PDF.
- DOCX.
- Markdown.
- TXT.

Nếu file lỗi, API trả `400 Bad Request` với error code tương ứng, ví dụ:

- `file_required`
- `file_too_large`
- `file_type_not_allowed`

### 4.4 Backend kiểm tra quyền folder

Backend kiểm tra user có quyền nhìn thấy folder đích hay không:

```text
folderPermissionService.CanViewFolderAsync(userId, folderId)
```

Nếu user không có quyền, API trả `403 Forbidden`.

Điểm quan trọng: hệ thống dùng folder permission để ngăn user upload vào vùng tri thức họ không được thấy.

### 4.5 Backend validate title

Backend trim `title`.

Nếu title rỗng, API trả:

```text
400 title_required
```

### 4.6 Backend tạo DocumentEntity

Backend tạo một bản ghi document mới:

```text
DocumentEntity
```

Các trường chính:

- `Id`: GUID mới.
- `FolderId`: folder user chọn.
- `Title`: title đã trim.
- `Description`: mô tả đã trim nếu có.
- `Status`: `PendingReview`.
- `CreatedByUserId`: user upload.
- `CreatedAt`: thời điểm upload.
- `UpdatedAt`: thời điểm upload.
- `CurrentVersionId`: null ở thời điểm mới upload.

Ở bước này, document mới chỉ là metadata nghiệp vụ. Nó chưa dùng cho AI Q&A.

### 4.7 Backend lưu file gốc

Backend gọi:

```text
fileStorageService.SaveDocumentVersionAsync(documentId, versionId, file)
```

File được lưu vào local storage theo cấu trúc:

```text
{storageRoot}/documents/{documentId}/{versionId}/{generated-safe-file-name}
```

Tên file lưu thật được tạo theo logic:

```text
{guid}-{safe-original-name}{extension}
```

Ví dụ:

```text
storage/documents/
  0f8...documentId/
    a12...versionId/
      8e7...-quy-trinh-thanh-toan.pdf
```

Backend lưu đường dẫn thật vào `DocumentVersionEntity.StoredFilePath`.

### 4.8 Backend tạo DocumentVersionEntity

Backend tạo version đầu tiên:

```text
DocumentVersionEntity
```

Các trường chính:

- `Id`: GUID mới.
- `DocumentId`: id của document cha.
- `VersionNumber`: `1`.
- `OriginalFileName`: tên file gốc.
- `StoredFilePath`: đường dẫn file đã lưu.
- `FileExtension`: extension, ví dụ `.pdf`, `.docx`.
- `FileSizeBytes`: dung lượng file.
- `ContentType`: content type từ upload.
- `Status`: `PendingReview`.
- `UploadedByUserId`: user upload.
- `CreatedAt`: thời điểm upload.
- `UpdatedAt`: thời điểm upload.

Ở bước này chưa có:

- `ExtractedTextPath`.
- `NormalizedTextPath`.
- `DocumentSummary`.
- `Language`.
- `DocumentType`.
- `TextHash`.
- `IndexedAt`.

Các trường đó chỉ có sau khi worker xử lý.

### 4.9 Backend lưu vào SQLite

Backend thêm vào DbContext:

```text
dbContext.Documents.Add(document)
dbContext.DocumentVersions.Add(version)
```

Sau đó gọi:

```text
SaveChangesAsync()
```

Từ thời điểm này SQLite có:

- Một document ở trạng thái `PendingReview`.
- Một document version ở trạng thái `PendingReview`.
- File gốc đã nằm trong local storage.

### 4.10 Backend ghi audit log

Backend ghi audit:

```text
Action: DocumentUploaded
EntityType: Document
EntityId: document.Id
Payload: title, folderId
```

Audit log giúp truy vết ai đã upload tài liệu nào vào folder nào.

### 4.11 API trả response

API trả:

```http
201 Created
```

Response là `DocumentDetailResponse`, gồm:

- Document id.
- Folder id.
- Folder path.
- Title.
- Description.
- Status.
- Current version id.
- Danh sách versions.

Tài liệu lúc này vẫn chưa thành tri thức. Nó mới nằm trong hàng chờ review.

## 5. Pha upload version mới

Nếu user upload version mới cho document đã tồn tại, API gọi:

```http
POST /api/documents/{id}/versions
```

Luồng gần giống upload document mới, nhưng khác ở vài điểm:

- Backend tìm document hiện có.
- Kiểm tra document chưa bị deleted.
- Kiểm tra user có quyền folder của document.
- Validate file.
- Tính `nextVersionNumber = max(existing version number) + 1`.
- Lưu file vào folder theo `documentId/versionId`.
- Tạo `DocumentVersionEntity` mới với trạng thái `PendingReview`.
- Nếu document chưa có `CurrentVersionId`, document giữ trạng thái `PendingReview`.
- Ghi audit `DocumentVersionUploaded`.

Version mới chưa thay thế current version cho đến khi reviewer approve.

Điều này quan trọng: nếu document đang có version 1 đã Indexed, user upload version 2 thì version 1 vẫn là current version cho AI Q&A cho đến khi version 2 được approve và xử lý.

## 6. Pha reviewer duyệt hoặc reject

### 6.1 Reviewer/Admin approve

Reviewer hoặc Admin gọi:

```http
POST /api/documents/{id}/approve
```

Request gồm:

```json
{
  "versionId": "..."
}
```

API yêu cầu role:

- `Admin`
- `Reviewer`

Backend tìm document và version tương ứng.

Nếu document không tồn tại:

```text
404 document_not_found
```

Nếu version không tồn tại:

```text
404 version_not_found
```

### 6.2 Backend cập nhật trạng thái version khi approve

Khi approve, backend cập nhật version:

- `Status = Approved`
- `RejectReason = null`
- `ReviewedByUserId = reviewerId`
- `ReviewedAt = now`
- `UpdatedAt = now`

### 6.3 Backend cập nhật trạng thái document khi approve

Backend cập nhật document:

- `CurrentVersionId = version.Id`
- `Status = Approved`
- `UpdatedAt = now`

Từ góc nhìn nghiệp vụ, reviewer đã chọn version này làm version hiện hành của document.

Nhưng về mặt tri thức AI, version này vẫn chưa sẵn sàng cho đến khi worker xử lý xong và version chuyển sang `Indexed`.

### 6.4 Backend tạo processing job

Backend thêm bản ghi:

```text
ProcessingJobEntity
```

Nội dung:

- `Id`: GUID mới.
- `JobType`: `ExtractAndEmbedDocument`.
- `TargetType`: `DocumentVersion`.
- `TargetId`: version id.
- `Status`: `Pending`.
- `Attempts`: `0`.
- `CreatedAt`: now.

Processing job là cầu nối giữa hành động approve và xử lý tài liệu bất đồng bộ.

Lý do dùng job:

- Upload/approve API không bị chậm vì phải extract/embed ngay.
- Nếu xử lý lỗi, job có thể retry.
- Worker có thể chạy nền theo polling.

### 6.5 Backend lưu DB và ghi audit

Backend gọi `SaveChangesAsync`.

Sau đó ghi audit:

```text
Action: DocumentApproved
EntityType: Document
EntityId: document.Id
Payload: versionId
```

API trả:

```http
204 No Content
```

### 6.6 Reviewer/Admin reject

Nếu reviewer reject:

```http
POST /api/documents/{id}/reject
```

Request gồm:

```json
{
  "versionId": "...",
  "reason": "Lý do reject"
}
```

Backend yêu cầu reason không rỗng.

Khi reject:

- `version.Status = Rejected`
- `version.RejectReason = reason`
- `version.ReviewedByUserId = reviewerId`
- `version.ReviewedAt = now`
- `version.UpdatedAt = now`

Nếu document chưa có current version:

- `document.Status = Rejected`

Backend ghi audit:

```text
Action: DocumentRejected
```

Không có processing job được tạo. Version bị reject không được extract, không được embed, không được dùng cho AI.

## 7. Pha background worker xử lý document

### 7.1 Worker chạy nền

Backend có hosted service:

```text
ProcessingJobWorker
```

Worker chạy vòng lặp:

1. Tìm job `Pending`.
2. Job phải còn số lần retry.
3. Order theo `Id`.
4. Lấy một job.
5. Chuyển job sang `Running`.
6. Gọi service xử lý tương ứng.
7. Nếu thành công, chuyển job sang `Succeeded`.
8. Nếu lỗi, retry hoặc chuyển `Failed`.

Poll interval được cấu hình bằng `BackgroundJobOptions.PollSeconds`.

### 7.2 Worker lấy job ExtractAndEmbedDocument

Với job:

```text
JobType = ExtractAndEmbedDocument
TargetType = DocumentVersion
```

Worker gọi:

```text
DocumentProcessingService.ProcessDocumentVersionAsync(versionId)
```

`TargetId` chính là `DocumentVersion.Id`.

### 7.3 Worker đánh dấu job Running

Trước khi xử lý, worker cập nhật job:

- `Status = Running`
- `Attempts += 1`
- `StartedAt = now`
- `FinishedAt = null`
- `ErrorMessage = null`

Sau đó lưu DB.

## 8. Pha xử lý document version

Toàn bộ phần này nằm trong:

```text
DocumentProcessingService.ProcessDocumentVersionAsync
```

### 8.1 Load document version

Service load:

- `DocumentVersion`
- `Document`
- `Folder` của document

Nếu không tìm thấy version hoặc document, service throw lỗi:

```text
Document version not found.
```

### 8.2 Chuyển version sang Processing

Service cập nhật:

- `version.Status = Processing`
- `version.UpdatedAt = now`

Sau đó lưu DB.

Từ thời điểm này, UI có thể hiển thị version đang được xử lý.

### 8.3 Extract text từ file gốc

Service gọi:

```text
textExtractor.ExtractAsync(version.StoredFilePath, version.FileExtension)
```

Logic extract hiện tại:

- `.txt`, `.md`, `.markdown`: đọc text trực tiếp bằng `File.ReadAllTextAsync`.
- `.docx`: dùng `DocumentFormat.OpenXml`, lấy text trong Word document.
- `.pdf`: dùng `UglyToad.PdfPig`, đọc text từng page.

Nếu extension không hỗ trợ, throw:

```text
Unsupported document extension
```

Nếu text rỗng hoặc chỉ whitespace, throw:

```text
Document has no extractable text.
```

### 8.4 Lưu extracted text ra file

Sau khi extract, service tạo file:

```text
{storageRoot}/documents/{documentId}/{versionId}/extracted.txt
```

Nội dung là raw text lấy từ file gốc.

Đường dẫn này được lưu vào:

```text
DocumentVersion.ExtractedTextPath
```

Lưu file extracted giúp hệ thống:

- Không cần extract lại mỗi lần generate wiki.
- Có thể debug nội dung extract.
- Có nguồn text trung gian cho các bước sau.

### 8.5 Normalize text

Service gọi:

```text
textNormalizer.Normalize(extractedText)
```

Normalizer làm các việc chính:

- Chuẩn hóa Unicode về Form C.
- Chuẩn hóa newline về `\n`.
- Xóa control characters không cần thiết.
- Trim cuối dòng.
- Collapse whitespace inline.
- Giảm nhiều blank lines liên tiếp.
- Phát hiện ký tự lỗi `replacement_character_found`.
- Phát hiện dấu hiệu encoding lỗi `possible_encoding_issue`.
- Cảnh báo nếu text bị co lại quá nhiều sau normalize.

Kết quả gồm:

- `Text`: text đã normalize.
- `Warnings`: danh sách cảnh báo xử lý.

### 8.6 Lưu normalized text ra file

Service tạo file:

```text
{storageRoot}/documents/{documentId}/{versionId}/normalized.txt
```

Đường dẫn này được lưu vào:

```text
DocumentVersion.NormalizedTextPath
```

Normalized text là nguồn ưu tiên dùng để:

- Detect sections.
- Chunk.
- Generate wiki draft.
- Tính hash.

### 8.7 Tính hash nội dung normalized

Service tính SHA-256 của normalized text:

```text
TextHash = SHA256(normalized.Text)
```

Hash được lưu vào:

```text
DocumentVersion.TextHash
```

Mục đích:

- Nhận diện nội dung đã xử lý.
- Hỗ trợ kiểm tra thay đổi nội dung.
- Hỗ trợ audit/debug/rebuild index sau này.

### 8.8 Detect sections

Service gọi:

```text
sectionDetector.Detect(normalized.Text)
```

Section detector tìm heading theo:

- Markdown heading, ví dụ `#`, `##`.
- Numbered heading, ví dụ `1.`, `1.2`, `IV.`
- Một số heading tiếng Việt thường gặp.

Nếu không tìm được heading, toàn bộ document được xem như một section fallback:

```text
Title = Document
Index = 0
Text = toàn bộ normalized text
```

Mỗi section có:

- `Index`
- `Title`
- `StartOffset`
- `EndOffset`
- `Text`

Số lượng section được lưu vào:

```text
DocumentVersion.SectionCount
```

### 8.9 Phân tích hiểu tài liệu

Service gọi:

```text
documentUnderstandingService.AnalyzeAsync(title, normalizedText, sections)
```

Nếu dùng mock service, hệ thống phân tích bằng heuristic.

Nếu dùng OpenAI-compatible service, hệ thống gửi prompt để AI trả JSON với schema:

```json
{
  "language": "vi|en|unknown",
  "documentType": "pricing|policy|procedure|technical|contract|faq|unknown",
  "summary": "string",
  "keyTopics": ["string"],
  "entities": ["string"],
  "effectiveDate": "ISO date or null",
  "sensitivity": "normal|internal|confidential",
  "qualityWarnings": ["string"]
}
```

Nếu AI trả JSON lỗi, service thử repair một lần. Nếu vẫn lỗi, fallback về heuristic.

Kết quả được lưu vào document version:

- `DocumentSummary`
- `Language`
- `DocumentType`
- `KeyTopicsJson`
- `EntitiesJson`
- `EffectiveDate`
- `Sensitivity`
- `QualityWarningsJson`

Những metadata này giúp:

- UI hiển thị document intelligence.
- Retrieval có thêm metadata.
- Keyword index có thêm topics/entities.
- Reviewer hiểu nhanh tài liệu.

### 8.10 Chunk tài liệu

Service gọi:

```text
chunker.Chunk(normalized.Text, sections)
```

Chiến lược chunk hiện tại:

- Target khoảng `2800` ký tự mỗi chunk.
- Overlap khoảng `350` ký tự.
- Nếu có sections, chunk theo từng section.
- Nếu section dài, chia thành nhiều chunk.
- Cố gắng cắt ở paragraph break nếu có.

Mỗi chunk có:

- `Index`
- `Text`
- `SectionTitle`
- `SectionIndex`
- `StartOffset`
- `EndOffset`

Ví dụ chunk id của document:

```text
{documentVersionId:N}-{chunkIndex}
```

### 8.11 Tạo embedding cho từng chunk

Với mỗi chunk, service gọi:

```text
embeddingService.CreateEmbeddingAsync(chunk.Text)
```

Nếu đang dùng mock:

- Tạo vector 64 chiều bằng token hash.
- Dùng được cho local/test nhưng không phải semantic embedding thật.

Nếu đang dùng OpenAI-compatible provider:

- Gọi AI embedding API theo cấu hình provider.
- Dimension lấy từ AI provider settings.

Mỗi chunk sau khi embedding trở thành:

```text
KnowledgeChunkRecord
```

Gồm:

- `Id`: chunk id.
- `Embedding`: vector float array.
- `Text`: chunk text.
- `Metadata`: metadata phục vụ search/filter/citation.

### 8.12 Metadata của document chunk

Mỗi document chunk được gắn metadata như:

```json
{
  "chunk_id": "...",
  "source_type": "document",
  "source_id": "documentVersionId",
  "document_id": "documentId",
  "document_version_id": "documentVersionId",
  "folder_id": "folderId",
  "title": "Tên tài liệu",
  "folder_path": "/Folder/SubFolder",
  "version_number": 1,
  "status": "approved",
  "visibility_scope": "folder",
  "chunk_index": 0,
  "section_title": "Tên section",
  "section_index": 0,
  "char_start": 0,
  "char_end": 2800,
  "language": "vi",
  "document_type": "procedure",
  "keywords": "topic1, topic2",
  "entities": "Entity1, Entity2",
  "sensitivity": "internal",
  "text_hash": "...",
  "created_at": "..."
}
```

Metadata này rất quan trọng khi retrieval:

- `source_type`: biết đây là document hay wiki.
- `document_id`: filter theo document.
- `folder_id`: filter theo quyền folder.
- `status`: chỉ lấy approved/published.
- `visibility_scope`: phân biệt folder/company visibility.
- `title`, `folder_path`, `section_title`: dùng cho citation.
- `language`, `document_type`, `keywords`, `entities`: dùng cho hiểu ngữ cảnh và keyword search.

## 9. Lưu vào vector DB

### 9.1 Đảm bảo Chroma collection tồn tại

Trước khi upsert, vector store gọi:

```text
EnsureCollectionAsync()
```

Nếu chưa có collection id, service gọi Chroma API để tạo hoặc lấy collection:

```http
POST /api/v2/tenants/{tenant}/databases/{database}/collections
```

Collection name lấy từ cấu hình `ChromaOptions.Collection`.

### 9.2 Upsert document chunks

Service gọi:

```text
vectorStore.UpsertChunksAsync(vectorChunks)
```

Payload gửi vào Chroma gồm:

- `ids`: danh sách chunk id.
- `embeddings`: vector embedding.
- `documents`: chunk text.
- `metadatas`: metadata của từng chunk.

Endpoint Chroma:

```http
POST /api/v2/tenants/{tenant}/databases/{database}/collections/{collectionId}/upsert
```

Sau bước này, document chunks đã có mặt trong vector DB và có thể được tìm bằng semantic search.

## 10. Lưu knowledge chunk ledger vào SQLite

Sau khi upsert vector DB, service gọi:

```text
chunkLedgerService.ReplaceChunksAsync(KnowledgeSourceType.Document, version.Id.ToString(), vectorChunks)
```

Ledger là bản sao có cấu trúc của chunks trong SQLite.

Trước khi ghi chunks mới, service xóa chunks cũ cùng:

```text
SourceType = Document
SourceId = documentVersionId
```

Sau đó thêm lại toàn bộ chunks mới.

Mỗi `KnowledgeChunkEntity` lưu:

- `ChunkId`
- `SourceType`
- `SourceId`
- `DocumentId`
- `DocumentVersionId`
- `WikiPageId`
- `FolderId`
- `VisibilityScope`
- `Status`
- `Title`
- `FolderPath`
- `SectionTitle`
- `SectionIndex`
- `ChunkIndex`
- `Text`
- `TextHash`
- `VectorId`
- `MetadataJson`
- `CreatedAt`
- `UpdatedAt`

Ledger có nhiều tác dụng:

- Cho phép rebuild hoặc kiểm tra index.
- Cho phép hiển thị/explain retrieval.
- Không phụ thuộc hoàn toàn vào vector DB.
- Là bản ghi dễ query bằng SQL.

## 11. Lưu keyword index vào SQLite

Service gọi:

```text
keywordIndexService.ReplaceChunksAsync(KnowledgeSourceType.Document, version.Id.ToString(), vectorChunks)
```

Keyword index cũng xóa dữ liệu cũ theo source rồi ghi lại.

Mỗi `KnowledgeChunkIndexEntity` lưu:

- Chunk id.
- Source type/source id.
- Document id/version id/wiki page id.
- Folder id.
- Visibility scope.
- Status.
- Title.
- Folder path.
- Section title.
- Text.
- Normalized text.

`NormalizedText` được tạo từ:

```text
title + section_title + keywords + entities + chunk text
```

Sau đó text được:

- Bỏ dấu tiếng Việt.
- Lowercase.
- Chỉ giữ chữ/số.
- Gộp whitespace.

Mục đích:

- Hỗ trợ keyword fallback search.
- Bổ sung semantic search.
- Tìm theo từ khóa, title, section, topics, entities.

## 12. Cập nhật document version sau xử lý thành công

Sau khi vector store, ledger và keyword index xử lý xong, service cập nhật version:

- `ExtractedTextPath = extractedTextPath`
- `NormalizedTextPath = normalizedTextPath`
- `SectionCount = sections.Count`
- `ProcessingWarningsJson = normalized.WarningsJson`
- `DocumentSummary = understanding.Summary` hoặc fallback summary
- `Language = understanding.Language`
- `DocumentType = understanding.DocumentType`
- `KeyTopicsJson = JSON`
- `EntitiesJson = JSON`
- `EffectiveDate = understanding.EffectiveDate`
- `Sensitivity = understanding.Sensitivity`
- `QualityWarningsJson = JSON`
- `TextHash = textHash`
- `Status = Indexed`
- `IndexedAt = now`
- `UpdatedAt = now`

Sau đó gọi:

```text
dbContext.SaveChangesAsync()
```

Đây là thời điểm document version chính thức hoàn tất xử lý.

Từ góc nhìn tri thức:

```text
DocumentVersionStatus.Indexed = có thể dùng làm nguồn AI Q&A
```

## 13. Worker kết thúc job

Nếu xử lý thành công:

- `job.Status = Succeeded`
- `job.ErrorMessage = null`
- `job.FinishedAt = now`

Nếu xử lý lỗi:

- Nếu còn retry: `job.Status = Pending`
- Nếu hết retry: `job.Status = Failed`
- `job.ErrorMessage = ex.Message`
- `job.FinishedAt = now` nếu failed hẳn

Nếu job `ExtractAndEmbedDocument` failed hẳn, worker cập nhật version:

```text
DocumentVersion.Status = ProcessingFailed
```

Điều này giúp UI/reviewer biết tài liệu đã duyệt nhưng không xử lý thành công.

## 14. Khi nào tài liệu được coi là tri thức?

Một tài liệu chỉ nên được coi là tri thức dùng cho AI khi:

1. Document chưa bị deleted.
2. Document status là `Approved`.
3. Document có `CurrentVersionId`.
4. Current version có status `Indexed`.
5. Chunks đã được ghi vào vector DB.
6. Chunks đã được ghi vào SQLite ledger/keyword index.
7. User hỏi có quyền với folder/document tương ứng.

Nếu version mới chỉ là `Approved` nhưng chưa `Indexed`, tài liệu chưa thật sự sẵn sàng cho RAG.

## 15. Tài liệu được dùng cho AI Q&A như thế nào?

Khi user hỏi AI, hệ thống không đọc file gốc trực tiếp.

Thay vào đó, luồng Q&A thường là:

1. User gửi câu hỏi.
2. Backend xác định quyền user từ SQLite.
3. Backend tạo embedding cho câu hỏi.
4. Backend query vector DB với filter quyền.
5. Backend có thể kết hợp keyword search.
6. Backend lấy các chunks phù hợp.
7. Backend đưa chunk text vào prompt làm context.
8. AI sinh câu trả lời dựa trên context.
9. Backend trả answer kèm citations.
10. Backend lưu interaction để phục vụ feedback/KPI.

Điểm quan trọng: file gốc chỉ là nguồn ban đầu. Khi Q&A, tri thức thực tế nằm ở:

- Vector DB.
- Knowledge chunk ledger.
- Keyword index.
- Metadata document/wiki trong SQLite.

## 16. Generate wiki draft từ document đã Indexed

### 16.1 Wiki không tự sinh ngay khi upload

Trong implementation hiện tại, wiki draft không tự động sinh khi document được Indexed.

Reviewer/Admin phải chủ động gọi:

```http
POST /api/wiki/generate
```

Request gồm:

```json
{
  "documentId": "...",
  "documentVersionId": "..."
}
```

API yêu cầu role:

- `Admin`
- `Reviewer`

### 16.2 Backend kiểm tra document version

`WikiService.GenerateDraftAsync` load document version kèm document và folder.

Các điều kiện bắt buộc:

1. Version tồn tại.
2. Document tồn tại.
3. Document chưa bị deleted.
4. `version.Status == Indexed`.
5. `document.CurrentVersionId == version.Id`.
6. Reviewer có quyền folder.

Nếu version chưa Indexed, API trả lỗi:

```text
document_version_not_indexed
```

Điều này đảm bảo wiki chỉ sinh từ tài liệu đã duyệt và đã index xong.

### 16.3 Backend đọc source text

Wiki generation ưu tiên dùng:

```text
version.NormalizedTextPath
```

Nếu không có normalized text, fallback sang:

```text
version.ExtractedTextPath
```

Nếu không có file text hoặc file rỗng, API lỗi:

```text
extracted_text_not_found
```

### 16.4 Tìm related documents

Trước khi sinh draft, service tìm tài liệu liên quan:

```text
FindRelatedDocumentsAsync
```

Luồng:

1. Lấy danh sách folder reviewer được phép thấy.
2. Chọn query text: ưu tiên `DocumentSummary`, fallback sang source text.
3. Trim query text còn khoảng 4000 ký tự.
4. Tạo embedding cho query text.
5. Query vector DB với filter:
   - FolderIds = folder reviewer được phép thấy.
   - IncludeCompanyVisible = true.
   - SourceTypes = `document`, `wiki`.
   - Statuses = `approved`, `published`.
6. Lấy tối đa 30 vector results.
7. Loại chính document nguồn.
8. Group theo `document_id`.
9. Lấy top candidates.
10. Query lại SQLite để đảm bảo document còn approved, current version, visible folder.
11. Trả tối đa 5 related documents.

Nếu bước này lỗi, service trả danh sách rỗng thay vì làm fail generate wiki.

Related documents được append vào cuối wiki draft trong section:

```markdown
## Related documents
```

### 16.5 AI sinh wiki draft

Service gọi:

```text
draftGenerationService.GenerateAsync(documentTitle, sourceText)
```

Nếu dùng mock:

- Sinh markdown đơn giản từ source.
- Tự tạo các section như Purpose, Scope, Audience, Procedure, FAQ.

Nếu dùng OpenAI-compatible provider:

- Gửi prompt yêu cầu AI trả JSON có schema.
- AI chỉ được dùng nội dung source document.
- Không được bịa owner, date, policy, price, SLA, approval rule.
- Nếu thiếu thông tin, ghi vào `missingInformation`.
- Service parse JSON rồi convert sang Markdown.
- Nếu JSON lỗi, repair một lần.
- Nếu vẫn lỗi, tạo fallback draft.

Wiki draft content sau cùng là Markdown.

### 16.6 Lưu WikiDraftEntity vào SQLite

Backend tạo:

```text
WikiDraftEntity
```

Các trường chính:

- `Id`: GUID mới.
- `SourceDocumentId`: document nguồn.
- `SourceDocumentVersionId`: version nguồn.
- `Title`: title document.
- `Content`: nội dung markdown do AI sinh + related documents.
- `Language`: ngôn ngữ draft.
- `MissingInformationJson`: thông tin còn thiếu.
- `RelatedDocumentsJson`: danh sách tài liệu liên quan.
- `Status`: `Draft`.
- `GeneratedByUserId`: reviewer.
- `CreatedAt`: now.
- `UpdatedAt`: now.

Sau đó lưu DB và ghi audit:

```text
Action: WikiDraftGenerated
EntityType: WikiDraft
```

Lúc này wiki draft chưa phải tri thức chính thức cho Q&A. Nó đang chờ reviewer publish.

## 17. Reviewer publish wiki draft

### 17.1 Gọi API publish

Reviewer/Admin gọi:

```http
POST /api/wiki/drafts/{id}/publish
```

Request gồm:

```json
{
  "visibilityScope": "Folder hoặc Company",
  "folderId": "...",
  "isCompanyPublicConfirmed": true
}
```

Nếu publish scope là company, bắt buộc:

```text
isCompanyPublicConfirmed = true
```

Nếu không, API trả:

```text
company_public_confirmation_required
```

### 17.2 Backend kiểm tra draft

Backend kiểm tra:

- Draft tồn tại.
- Draft có source document.
- Draft status là `Draft`.
- Nếu folder visibility, reviewer có quyền folder.
- Folder publish tồn tại.

Nếu draft đã Published/Rejected/Archived, không thể publish lại.

### 17.3 Tạo WikiPageEntity

Backend tạo:

```text
WikiPageEntity
```

Các trường chính:

- `Id`: GUID mới.
- `SourceDraftId`: draft id.
- `SourceDocumentId`: document nguồn.
- `SourceDocumentVersionId`: version nguồn.
- `Title`: title draft.
- `Content`: content draft.
- `Language`: language draft.
- `VisibilityScope`: `Folder` hoặc `Company`.
- `FolderId`: folder nếu visibility theo folder.
- `IsCompanyPublicConfirmed`: xác nhận public nội bộ.
- `PublishedByUserId`: reviewer.
- `PublishedAt`: now.
- `CreatedAt`: now.
- `UpdatedAt`: now.

Đồng thời cập nhật draft:

- `Status = Published`
- `ReviewedByUserId = reviewerId`
- `ReviewedAt = now`
- `UpdatedAt = now`

Sau đó lưu DB.

Từ góc nhìn nghiệp vụ, wiki đã được publish. Nhưng để dùng tốt trong RAG, wiki còn phải được index.

## 18. Index wiki page vào vector DB

Sau khi lưu `WikiPageEntity`, service gọi:

```text
IndexWikiPageAsync(page, folderPath, draft)
```

### 18.1 Detect section cho wiki

Wiki content là Markdown, nên section detector thường phát hiện các heading:

- `# Title`
- `## Purpose`
- `## Scope`
- `## Main content`
- `## Procedure`
- `## FAQ`
- `## Related documents`

### 18.2 Chunk wiki content

Service gọi cùng `TextChunker`:

```text
chunker.Chunk(page.Content, sections)
```

Chunk wiki cũng dùng:

- Target 2800 ký tự.
- Overlap 350 ký tự.
- Section title/index.
- Char start/end.

Wiki chunk id:

```text
{wikiPageId:N}-{chunkIndex}
```

### 18.3 Tạo embedding cho wiki chunks

Với từng wiki chunk, service gọi:

```text
embeddingService.CreateEmbeddingAsync(chunk.Text)
```

Embedding của wiki dùng cùng provider với document.

### 18.4 Metadata của wiki chunk

Mỗi wiki chunk có metadata:

```json
{
  "chunk_id": "...",
  "source_type": "wiki",
  "source_id": "wikiPageId",
  "wiki_page_id": "wikiPageId",
  "document_id": "sourceDocumentId",
  "document_version_id": "sourceDocumentVersionId",
  "folder_id": "folderId hoặc rỗng",
  "title": "Wiki title",
  "folder_path": "/Folder/SubFolder",
  "status": "published",
  "visibility_scope": "company hoặc folder",
  "chunk_index": 0,
  "section_title": "Purpose",
  "section_index": 0,
  "char_start": 0,
  "char_end": 2800,
  "related_document_count": 3,
  "missing_information_count": 2,
  "created_at": "..."
}
```

### 18.5 Upsert wiki chunks vào Chroma

Service gọi:

```text
vectorStore.UpsertChunksAsync(vectorChunks)
```

Wiki chunks được lưu vào cùng collection với document chunks, nhưng khác metadata:

```text
source_type = wiki
status = published
```

Nhờ vậy retrieval có thể ưu tiên wiki trước document.

### 18.6 Ghi wiki chunks vào SQLite ledger

Service gọi:

```text
chunkLedgerService.ReplaceChunksAsync(KnowledgeSourceType.Wiki, page.Id.ToString(), vectorChunks)
```

Ledger lưu source type là `Wiki`.

### 18.7 Ghi wiki chunks vào keyword index

Service gọi:

```text
keywordIndexService.ReplaceChunksAsync(KnowledgeSourceType.Wiki, page.Id.ToString(), vectorChunks)
```

Từ lúc này wiki page vừa:

- Có record nghiệp vụ trong SQLite.
- Có chunks trong vector DB.
- Có chunks trong ledger.
- Có keyword index.

Wiki published đã trở thành nguồn tri thức chuẩn hóa.

### 18.8 Ghi audit publish

Backend ghi audit:

```text
Action: WikiPublished
EntityType: WikiPage
EntityId: page.Id
Payload: draftId, visibilityScope, folderId
```

## 19. Vì sao wiki là lớp tri thức ưu tiên?

Document gốc thường có vấn đề:

- Dài.
- Trùng lặp.
- Không đồng nhất format.
- Có thể chứa phần thừa.
- Người dùng khó đọc nhanh.

Wiki published là nội dung đã qua:

1. AI tổng hợp.
2. Reviewer kiểm tra.
3. Reviewer publish.
4. Index riêng vào vector DB.

Vì vậy khi Q&A, hệ thống ưu tiên:

1. Wiki published.
2. Fallback sang document approved/indexed.

Điều này giúp câu trả lời ổn định hơn, ngắn gọn hơn và ít phụ thuộc vào tài liệu gốc rời rạc.

## 20. Luồng dữ liệu đầy đủ dạng sequence

```text
User
  |
  | POST /api/documents
  v
DocumentsController
  |
  | validate JWT
  | validate file
  | check folder permission
  | validate title
  v
FileStorageService
  |
  | save original file
  v
Local Storage
  |
  v
DocumentsController
  |
  | create DocumentEntity
  | create DocumentVersionEntity
  | save SQLite
  | audit DocumentUploaded
  v
SQLite
  |
  | Document = PendingReview
  | Version = PendingReview
  v
Reviewer/Admin
  |
  | POST /api/documents/{id}/approve
  v
DocumentsController
  |
  | Version = Approved
  | Document = Approved
  | Document.CurrentVersionId = versionId
  | create ProcessingJob ExtractAndEmbedDocument
  | audit DocumentApproved
  v
SQLite
  |
  | Job = Pending
  v
ProcessingJobWorker
  |
  | poll pending job
  | Job = Running
  v
DocumentProcessingService
  |
  | Version = Processing
  | extract text from original file
  | write extracted.txt
  | normalize text
  | write normalized.txt
  | compute text hash
  | detect sections
  | analyze document understanding
  | chunk text
  | create embedding per chunk
  v
Chroma Vector DB
  |
  | upsert document chunks
  v
SQLite
  |
  | replace KnowledgeChunks ledger
  | replace KeywordIndex
  | Version = Indexed
  | Job = Succeeded
  v
Document is ready for AI Q&A
```

Wiki flow:

```text
Reviewer/Admin
  |
  | POST /api/wiki/generate
  v
WikiService
  |
  | verify document version is Indexed
  | read normalized.txt
  | find related documents from vector DB
  | call AI to generate structured wiki draft
  | save WikiDraftEntity
  | audit WikiDraftGenerated
  v
SQLite
  |
  | WikiDraft = Draft
  v
Reviewer/Admin
  |
  | review draft
  | POST /api/wiki/drafts/{id}/publish
  v
WikiService
  |
  | create WikiPageEntity
  | WikiDraft = Published
  | detect wiki sections
  | chunk wiki content
  | create embedding per wiki chunk
  v
Chroma Vector DB
  |
  | upsert wiki chunks
  v
SQLite
  |
  | replace wiki KnowledgeChunks ledger
  | replace wiki KeywordIndex
  | audit WikiPublished
  v
Wiki is ready as preferred knowledge source
```

## 21. Các bảng dữ liệu bị ảnh hưởng

### Khi upload document

Ghi hoặc cập nhật:

- `Documents`
- `DocumentVersions`
- `AuditLogs`

Ghi file:

- Original uploaded file.

Chưa ghi:

- Vector DB.
- Knowledge chunks.
- Keyword index.
- Wiki draft/page.

### Khi approve document

Cập nhật:

- `Documents`
- `DocumentVersions`

Tạo:

- `ProcessingJobs`
- `AuditLogs`

Chưa chắc đã ghi vector DB ngay, vì worker xử lý sau.

### Khi processing thành công

Cập nhật:

- `DocumentVersions`
- `ProcessingJobs`

Ghi hoặc thay thế:

- `KnowledgeChunks`
- `KnowledgeChunkIndexes`
- Chroma collection records.

Ghi file:

- `extracted.txt`
- `normalized.txt`

### Khi generate wiki draft

Đọc:

- `DocumentVersions`
- `Documents`
- `Folders`
- `KnowledgeChunks` hoặc vector DB tùy related search.
- `normalized.txt` hoặc `extracted.txt`.

Ghi:

- `WikiDrafts`
- `AuditLogs`

Chưa ghi:

- Wiki page.
- Wiki chunks vào vector DB.

### Khi publish wiki

Ghi hoặc cập nhật:

- `WikiPages`
- `WikiDrafts`
- `KnowledgeChunks`
- `KnowledgeChunkIndexes`
- `AuditLogs`
- Chroma collection records.

## 22. Các điểm bảo mật và phân quyền trong luồng

### Upload

User chỉ upload được vào folder họ có quyền view.

### Download

Khi download file, backend:

- Kiểm tra token.
- Kiểm tra document tồn tại.
- Kiểm tra user có quyền folder.
- Resolve path và đảm bảo path nằm trong storage root.
- Trả file qua API.

File không được serve trực tiếp từ public web root.

### Review

Chỉ `Admin` và `Reviewer` được approve/reject document.

### Wiki

Chỉ `Admin` và `Reviewer` được generate/publish/reject wiki draft.

Khi publish company-wide, reviewer phải xác nhận:

```text
IsCompanyPublicConfirmed = true
```

### Retrieval

Vector DB chỉ hỗ trợ filter nhanh. Quyền thật vẫn nằm ở SQLite.

Metadata như `folder_id`, `visibility_scope`, `status`, `source_type` giúp vector query giảm phạm vi kết quả, nhưng backend vẫn cần coi SQLite là nguồn sự thật về permission.

## 23. Các tình huống lỗi thường gặp

### File không hợp lệ

Xảy ra ở upload:

- Không có file.
- File rỗng.
- File quá lớn.
- Extension không hỗ trợ.

Kết quả:

- Không tạo document.
- Không tạo version.
- Không lưu file.

### User không có quyền folder

Xảy ra khi upload hoặc generate/publish wiki theo folder.

Kết quả:

- API trả `403 Forbidden`.
- Không ghi dữ liệu nghiệp vụ mới.

### Reviewer reject

Version chuyển `Rejected`.

Kết quả:

- Không tạo processing job.
- Không index.
- Không dùng cho AI.

### Extract text rỗng

Worker throw:

```text
Document has no extractable text.
```

Kết quả:

- Job retry nếu còn attempts.
- Nếu hết retry, job `Failed`.
- Version `ProcessingFailed`.

### AI understanding trả JSON lỗi

Service thử repair.

Nếu repair vẫn lỗi:

- Fallback sang heuristic.
- Version vẫn có thể Indexed.
- `QualityWarningsJson` có thể chứa cảnh báo.

### Vector DB lỗi

Nếu upsert Chroma lỗi:

- Processing service throw.
- Job retry hoặc failed.
- Version không chuyển Indexed nếu toàn bộ flow chưa hoàn tất.

### Generate wiki từ version chưa Indexed

API trả:

```text
document_version_not_indexed
```

Lý do:

- Wiki phải sinh từ source text đã extract/normalize.
- Version phải là current version.
- Version phải đã được index để đảm bảo tài liệu đã qua pipeline tri thức.

## 24. Ranh giới giữa document knowledge và wiki knowledge

Document knowledge:

- Sinh từ file gốc đã approved.
- Có chunks source_type `document`.
- Status metadata là `approved`.
- Visibility thường theo folder.
- Dùng làm nguồn gốc và fallback.

Wiki knowledge:

- Sinh từ document đã Indexed.
- Được reviewer publish.
- Có chunks source_type `wiki`.
- Status metadata là `published`.
- Visibility có thể theo folder hoặc company.
- Được ưu tiên trong Q&A.

## 25. Kết luận

Luồng upload đến tri thức của hệ thống hiện tại có hai lớp:

1. Lớp document knowledge: upload -> review -> processing job -> extract -> normalize -> chunk -> embedding -> vector DB -> SQLite ledger/keyword index -> Indexed.
2. Lớp wiki knowledge: generate draft từ document Indexed -> reviewer publish -> chunk wiki -> embedding -> vector DB -> SQLite ledger/keyword index -> Published.

Điểm thiết kế quan trọng là hệ thống không tin file upload ngay lập tức. File phải qua review, xử lý nền, indexing và kiểm soát quyền trước khi trở thành nguồn tri thức cho AI.
