# Luồng người dùng hỏi AI đến khi hệ thống trả câu trả lời

Tài liệu này mô tả chi tiết luồng hiện tại khi người dùng đặt câu hỏi cho Internal Knowledge Copilot: backend kiểm tra quyền, hiểu câu hỏi, tìm tri thức liên quan trong vector DB và keyword index, lọc kết quả theo quyền, rerank, đóng gói context, gọi AI provider để sinh câu trả lời có nguồn, sau đó lưu lịch sử hỏi đáp và citations.

## 1. Tổng quan ngắn

Khi user hỏi hệ thống, backend không đưa toàn bộ tài liệu vào AI. Backend làm theo luồng RAG:

1. User gửi câu hỏi từ UI.
2. Backend xác thực user từ JWT.
3. Backend validate câu hỏi và phạm vi hỏi.
4. Backend kiểm tra quyền theo folder/document.
5. Backend phân tích câu hỏi thành normalized question và keywords.
6. Backend lấy danh sách folder user được phép xem.
7. Backend tạo embedding cho câu hỏi.
8. Backend build retrieval filter theo scope và quyền.
9. Backend query vector DB.
10. Backend query keyword index.
11. Backend merge kết quả vector và keyword.
12. Backend chuyển metadata thành knowledge chunks.
13. Backend lọc lại kết quả bằng quyền từ SQLite.
14. Backend loại các nguồn không hợp lệ như document version không current/indexed hoặc wiki bị archived.
15. Backend rerank chunks theo source priority, keyword match, phrase match, scope match và vector distance.
16. Backend đóng gói tối đa 8 chunks làm context.
17. Backend gọi AnswerGenerationService để sinh câu trả lời.
18. AI provider trả JSON answer có confidence, citations, missing information, conflicts, follow-ups.
19. Backend map citations AI trả về sang source thật.
20. Backend lưu `AiInteraction` và `AiInteractionSources` vào SQLite.
21. Backend trả answer, flags và citations về frontend.

## 2. Endpoint chính

User hỏi qua API:

```http
POST /api/ai/ask
```

Controller xử lý:

```text
src/backend/InternalKnowledgeCopilot.Api/Modules/Ai/AiController.cs
```

Service xử lý nghiệp vụ:

```text
src/backend/InternalKnowledgeCopilot.Api/Modules/Ai/AiQuestionService.cs
```

## 3. Request người dùng gửi lên

Request model:

```text
AskQuestionRequest
```

Gồm:

```json
{
  "question": "Câu hỏi của người dùng",
  "scopeType": "All | Folder | Document",
  "folderId": "nullable guid",
  "documentId": "nullable guid"
}
```

Ý nghĩa:

- `Question`: câu hỏi tự nhiên của user.
- `ScopeType = All`: hỏi trong toàn bộ tri thức user được phép xem.
- `ScopeType = Folder`: chỉ hỏi trong một folder cụ thể.
- `ScopeType = Document`: chỉ hỏi trong một document cụ thể.
- `FolderId`: bắt buộc nếu scope là Folder.
- `DocumentId`: bắt buộc nếu scope là Document.

## 4. Response trả về frontend

Response model:

```text
AskQuestionResponse
```

Gồm:

```json
{
  "interactionId": "...",
  "answer": "...",
  "needsClarification": false,
  "confidence": "high | medium | low",
  "missingInformation": [],
  "conflicts": [],
  "suggestedFollowUps": [],
  "citations": []
}
```

Ý nghĩa:

- `InteractionId`: id lượt hỏi, dùng để gửi feedback sau đó.
- `Answer`: câu trả lời cuối cùng.
- `NeedsClarification`: true nếu hệ thống cần user hỏi rõ hơn.
- `Confidence`: độ tự tin do answer service chuẩn hóa.
- `MissingInformation`: thông tin còn thiếu trong nguồn.
- `Conflicts`: thông tin mâu thuẫn nếu phát hiện.
- `SuggestedFollowUps`: gợi ý câu hỏi tiếp theo.
- `Citations`: danh sách nguồn đã dùng.

Mỗi citation có:

```json
{
  "sourceType": "Document | Wiki | Correction",
  "title": "Tên nguồn",
  "folderPath": "/Folder/SubFolder",
  "sectionTitle": "Tên section",
  "excerpt": "Đoạn trích ngắn"
}
```

## 5. Các loại nguồn tri thức có thể được retrieval

Luồng Q&A hiện tại có thể tìm trong 3 loại source:

```text
correction
document
wiki
```

Tương ứng enum:

```text
KnowledgeSourceType.Correction
KnowledgeSourceType.Document
KnowledgeSourceType.Wiki
```

Ý nghĩa:

- `Correction`: tri thức sửa lỗi/đính chính đã được approve.
- `Wiki`: wiki đã publish, là nguồn được ưu tiên cao.
- `Document`: document gốc đã approve, current version và đã Indexed.

Source priority khi rerank:

```text
Correction: +100
Wiki:       +45
Document:   +20
```

Vì vậy nếu có correction hoặc wiki liên quan, chúng thường được chọn trước document gốc.

## 6. Pha xác thực request

### 6.1 Controller lấy user id từ JWT

`AiController.Ask` lấy user id từ:

```text
ClaimTypes.NameIdentifier
```

Nếu không parse được GUID:

```http
401 Unauthorized
```

Error:

```text
invalid_token
```

### 6.2 Controller gọi service

Sau khi có user id, controller gọi:

```text
aiQuestionService.AskAsync(userId, request)
```

Từ đây toàn bộ logic RAG nằm trong `AiQuestionService`.

## 7. Pha validate câu hỏi

Service trim câu hỏi:

```text
question = request.Question.Trim()
```

Nếu câu hỏi rỗng:

```text
ArgumentException("question_required")
```

Controller map thành:

```http
400 Bad Request
```

Error:

```text
question_required
```

## 8. Pha validate scope

Service gọi:

```text
ValidateScopeAsync(userId, request)
```

### 8.1 Scope All

Nếu `ScopeType = All`:

- Không yêu cầu `FolderId`.
- Không yêu cầu `DocumentId`.
- Hệ thống sẽ tìm trong tất cả folder user được phép xem.
- Hệ thống cũng cho phép nguồn company-wide nếu nguồn đó hợp lệ.

### 8.2 Scope Folder

Nếu `ScopeType = Folder`:

- `FolderId` bắt buộc.
- Backend kiểm tra user có quyền view folder đó.

Nếu thiếu folder id:

```text
folder_required
```

Nếu user không có quyền:

```http
403 Forbidden
```

### 8.3 Scope Document

Nếu `ScopeType = Document`:

- `DocumentId` bắt buộc.
- Backend tìm document trong SQLite.
- Document phải chưa bị deleted.
- Backend kiểm tra user có quyền view folder chứa document.

Nếu thiếu document id:

```text
document_required
```

Nếu document không tồn tại:

```text
document_not_found
```

Nếu user không có quyền folder chứa document:

```http
403 Forbidden
```

Điểm quan trọng: scope validation chỉ xác nhận user được phép hỏi trong phạm vi đó. Sau retrieval, backend vẫn lọc lại từng chunk bằng SQLite.

## 9. Pha hiểu câu hỏi ở mức đơn giản

Service gọi:

```text
UnderstandQuery(question)
```

Hiện tại đây không phải LLM query rewriting phức tạp. Nó làm hai việc:

1. Tạo normalized question.
2. Tách keywords.

### 9.1 Normalize câu hỏi

Hệ thống normalize text để phục vụ keyword matching:

- Chuyển `đ` thành `d`.
- Normalize Unicode về FormD.
- Bỏ dấu tiếng Việt.
- Lowercase.
- Chỉ giữ chữ và số.
- Các ký tự khác thành khoảng trắng.
- Gộp whitespace.

Ví dụ:

```text
"Quy trình duyệt thanh toán là gì?"
```

có thể thành:

```text
"quy trinh duyet thanh toan la gi"
```

### 9.2 Tách keywords

Hệ thống split normalized question thành token.

Sau đó:

- Bỏ token dưới 2 ký tự.
- Bỏ stop words tiếng Anh và tiếng Việt không dấu.
- Deduplicate.
- Lấy tối đa 16 keywords.

Ví dụ:

```text
Question: "Quy trình duyệt thanh toán là gì?"
Keywords: ["quy", "trinh", "duyet", "thanh", "toan"]
```

Keywords này dùng cho:

- Keyword index search.
- Scoring/rerank chunk.
- Retrieval explain.

## 10. Lấy quyền folder của user

Service gọi:

```text
folderPermissionService.GetVisibleFolderIdsAsync(userId)
```

Kết quả là danh sách folder id user được phép xem.

Danh sách này được dùng ở nhiều lớp:

1. Build vector DB filter.
2. Build keyword index filter.
3. Lọc lại result bằng SQLite.
4. Kiểm tra document current version.
5. Kiểm tra wiki folder visibility.
6. Kiểm tra correction visibility.

## 11. Tạo embedding cho câu hỏi

Service gọi:

```text
embeddingService.CreateEmbeddingAsync(question)
```

Nếu dùng mock:

- Tạo vector 64 chiều bằng token hash.
- Dùng được cho local/test.

Nếu dùng OpenAI-compatible provider:

- Gọi embedding API thật.
- Dimension lấy theo AI provider settings.

Embedding này dùng để tìm semantic similarity trong vector DB.

## 12. Build retrieval filter

Service gọi:

```text
BuildKnowledgeQueryFilter(visibleFolderIds, request)
```

Filter gồm:

```text
FolderIds
DocumentId
IncludeCompanyVisible
SourceTypes
Statuses
```

Source types mặc định:

```text
correction, document, wiki
```

Statuses mặc định:

```text
approved, published
```

### 12.1 Filter cho Scope All

Nếu hỏi toàn bộ:

```text
FolderIds = tất cả folder user được phép thấy
IncludeCompanyVisible = true
SourceTypes = correction, document, wiki
Statuses = approved, published
DocumentId = null
```

Ý nghĩa:

- Lấy nguồn trong các folder user có quyền.
- Lấy thêm nguồn company-wide nếu có.

### 12.2 Filter cho Scope Folder

Nếu hỏi trong folder:

```text
FolderIds = [folderId user chọn]
IncludeCompanyVisible = false
SourceTypes = correction, document, wiki
Statuses = approved, published
DocumentId = null
```

Ý nghĩa:

- Chỉ lấy nguồn trong folder được chọn.
- Không lấy nguồn company-wide ngoài folder.

### 12.3 Filter cho Scope Document

Nếu hỏi trong document:

```text
FolderIds = tất cả folder user được phép thấy
DocumentId = documentId user chọn
IncludeCompanyVisible = true
SourceTypes = correction, document, wiki
Statuses = approved, published
```

Ý nghĩa:

- Lọc theo document id.
- Vẫn cho phép wiki/correction liên quan document đó nếu hợp lệ.
- Folder visibility vẫn dựa trên folder user được phép thấy.

## 13. Query vector DB

Service gọi:

```text
vectorStore.QueryAsync(queryEmbedding, SearchLimit, knowledgeFilter)
```

Giá trị hiện tại:

```text
SearchLimit = 50
```

Vector store hiện tại là:

```text
ChromaKnowledgeVectorStore
```

### 13.1 Vector DB nhận gì?

Chroma query nhận:

- `query_embeddings`: vector của câu hỏi.
- `n_results`: số kết quả tối đa.
- `include`: documents, metadatas, distances.
- `where`: filter metadata.

### 13.2 Metadata filter trong Chroma

Filter được build từ:

- `source_type`.
- `status`.
- `document_id` nếu có.
- `folder_id`.
- `visibility_scope`.

Nếu `IncludeCompanyVisible = true`, filter cho phép:

```text
visibility_scope = company
OR folder_id in visibleFolderIds
```

Nếu `IncludeCompanyVisible = false`, filter chỉ lấy:

```text
folder_id in selectedFolderIds
```

### 13.3 Vector result trả về

Mỗi result có:

```text
KnowledgeVectorSearchResult
```

Gồm:

- `Id`: id record trong vector DB.
- `Text`: chunk text.
- `Metadata`: metadata chunk.
- `Distance`: khoảng cách vector, càng thấp thường càng gần.

Ở bước này, kết quả mới là candidates. Chưa được tin hoàn toàn.

## 14. Query keyword index

Song song về mặt logic với vector search, service gọi:

```text
keywordIndexService.SearchAsync(keywords, KeywordSearchLimit, knowledgeFilter)
```

Giá trị hiện tại:

```text
KeywordSearchLimit = 20
```

Keyword index nằm trong SQLite.

### 14.1 Keyword search dùng gì?

Keyword index dùng bảng:

```text
KnowledgeChunkIndexes
```

Mỗi chunk index lưu normalized text đã bỏ dấu và lowercase.

Normalized text được tạo từ:

```text
title + section_title + keywords + entities + chunk text
```

### 14.2 Keyword scoring

Với mỗi keyword:

- Nếu keyword xuất hiện trong normalized chunk text: +10.
- Nếu keyword xuất hiện trong title/section title: +4.

Nếu toàn bộ cụm keywords xuất hiện liền nhau:

- +20.

Kết quả được sort:

1. Score giảm dần.
2. UpdatedAt giảm dần.

Keyword search giúp bắt các trường hợp semantic embedding chưa tốt, đặc biệt khi user hỏi bằng từ khóa cụ thể như mã chính sách, tên quy trình, tên sản phẩm, từ viết tắt.

## 15. Merge vector results và keyword results

Service gọi:

```text
MergeCandidateResults(vectorResults, keywordResults)
```

Logic:

- Nối vector results và keyword results.
- Dedupe theo `chunk_id` trong metadata.
- Nếu không có `chunk_id`, dùng result id.
- Giữ thứ tự ưu tiên vector trước, keyword bổ sung sau.

Mục tiêu:

- Không đưa cùng một chunk vào hai lần.
- Vẫn tận dụng cả semantic search và keyword search.

Trong endpoint explain, service còn ghi nhận candidate đến từ:

- `Vector`
- `Keyword`
- `Vector+Keyword`

## 16. Chuyển result thành RetrievedKnowledgeChunk

Mỗi `KnowledgeVectorSearchResult` được convert bằng:

```text
ToRetrievedChunk(result)
```

Backend đọc metadata để tạo:

```text
RetrievedKnowledgeChunk
```

Gồm:

- `SourceType`
- `SourceId`
- `DocumentId`
- `DocumentVersionId`
- `WikiPageId`
- `FolderId`
- `VisibilityScope`
- `Title`
- `FolderPath`
- `SectionTitle`
- `SectionIndex`
- `Text`
- `Distance`

Nếu metadata thiếu hoặc source type không parse được, candidate bị reject.

Điểm quan trọng: chất lượng metadata khi index document/wiki ảnh hưởng trực tiếp đến retrieval, permission filter và citation.

## 17. Lọc sơ bộ theo requested scope và folder visibility

Service gọi:

```text
AnalyzeCandidateAccessAsync(...)
```

Với từng candidate:

### 17.1 Kiểm tra metadata hợp lệ

Nếu không parse được source type hoặc thiếu source id:

```text
Rejected because required retrieval metadata is missing or invalid.
```

### 17.2 Kiểm tra candidate nằm trong scope user hỏi

Logic:

```text
All      -> cho qua
Folder   -> chunk.FolderId == request.FolderId
Document -> chunk.DocumentId == request.DocumentId
```

Nếu không đúng:

```text
Rejected because it is outside the requested scope.
```

### 17.3 Kiểm tra user có quyền thấy folder

Logic:

- Nếu `visibility_scope = company`: cho qua bước folder visibility.
- Nếu không: `chunk.FolderId` phải nằm trong `visibleFolderIds`.

Nếu không:

```text
Rejected because its folder is not visible to the current user.
```

Lưu ý: Đây vẫn chưa phải kiểm tra cuối cùng. Sau bước này backend còn kiểm tra nguồn trong SQLite.

## 18. Lọc lại bằng SQLite theo loại source

Sau khi có candidates sơ bộ, service query SQLite để xác thực từng loại source.

Đây là lớp bảo vệ quan trọng vì vector DB chỉ là index, không phải nguồn sự thật.

### 18.1 Kiểm tra document chunks

Backend gom các `DocumentVersionId` từ document chunks.

Sau đó query:

- Version id nằm trong candidates.
- `version.Status == Indexed`.
- Version có document.
- `document.CurrentVersionId == version.Id`.
- Document chưa bị deleted.
- Folder của document nằm trong visibleFolderIds.

Chỉ các version thỏa điều kiện mới được giữ.

Nếu không thỏa, chunk bị reject:

```text
Rejected because the document chunk is not the current indexed version, was deleted, or is outside visible folders.
```

Điều này đảm bảo:

- Không dùng version cũ.
- Không dùng version chưa index xong.
- Không dùng document đã xóa.
- Không dùng document ngoài quyền user.

### 18.2 Kiểm tra wiki chunks

Backend gom các `WikiPageId` từ wiki chunks.

Sau đó query:

- Wiki page id nằm trong candidates.
- `ArchivedAt == null`.
- Nếu visibility là Company:
  - `IsCompanyPublicConfirmed == true`.
- Nếu visibility là Folder:
  - `FolderId != null`.
  - Folder nằm trong visibleFolderIds.

Khi kiểm tra thêm theo request:

- Scope Folder: wiki page folder phải bằng folder user chọn.
- Scope Document: wiki page source document phải bằng document user chọn.
- Nếu wiki visibility là Folder, chunk folder id phải khớp page folder id.

Nếu không thỏa, chunk bị reject:

```text
Rejected because the wiki page is archived, not visible, or outside the requested scope.
```

### 18.3 Kiểm tra correction chunks

Backend gom correction id từ source id.

Sau đó query:

- Correction id nằm trong candidates.
- `Status == Approved`.
- Nếu visibility là Company: cho phép.
- Nếu visibility là Folder:
  - Folder id không null.
  - Folder nằm trong visibleFolderIds.

Khi kiểm tra thêm theo request:

- Scope Folder: correction folder phải bằng folder user chọn.
- Scope Document: correction document phải bằng document user chọn.

Nếu không thỏa:

```text
Rejected because the correction is not approved, not visible, or outside the requested scope.
```

## 19. Rerank kết quả

Sau khi lọc quyền và tính hợp lệ, service gọi:

```text
RerankAndPackContext(chunks, queryUnderstanding, request)
```

Mỗi chunk được chấm điểm bởi:

```text
ScoreChunk
```

Score gồm:

1. Source priority.
2. Keyword match.
3. All keyword match.
4. Full phrase match.
5. Requested scope match.
6. Vector distance score.

### 19.1 Source priority

Điểm theo loại source:

```text
Correction: +100
Wiki:       +45
Document:   +20
```

Ý nghĩa:

- Correction là tri thức sửa lỗi đã approve nên ưu tiên cao nhất.
- Wiki là tri thức đã reviewer publish nên ưu tiên hơn document.
- Document là nguồn gốc/fallback.

### 19.2 Keyword match

Backend normalize:

```text
chunk.Title + chunk.SectionTitle + chunk.Text
```

Sau đó so với query keywords.

Mỗi keyword match:

```text
+12
```

Nếu tất cả keywords đều match:

```text
+20
```

### 19.3 Full phrase match

Nếu normalized chunk text chứa toàn bộ normalized question:

```text
+18
```

### 19.4 Scope match

Nếu user hỏi scope Folder và chunk đúng folder:

```text
+20
```

Nếu user hỏi scope Document và chunk đúng document:

```text
+20
```

### 19.5 Vector distance score

Nếu result có distance:

```text
distanceScore = max(0, 1 - min(distance, 1))
```

Điểm này thường nhỏ hơn keyword/source priority, nhưng giúp phân biệt các kết quả semantic gần nhau.

### 19.6 Sort kết quả

Chunks được sort:

1. Score giảm dần.
2. Original index tăng dần.

Original index giúp giữ tính ổn định nếu score bằng nhau.

## 20. Đóng gói context

Sau khi rerank, backend không đưa mọi chunk vào prompt.

Giới hạn hiện tại:

```text
MaxContextChunks = 8
MaxChunksPerKnowledgeItem = 3
```

### 20.1 Tránh duplicate cùng section/source

Backend tạo exact source key:

```text
SourceType + SourceId + SectionIndex + normalized SectionTitle
```

Nếu một exact source key đã được chọn, chunk sau trùng key sẽ bị bỏ.

### 20.2 Giới hạn số chunk trên mỗi knowledge item

Packing key:

- Nếu có `DocumentId`: dùng `document:{documentId}`.
- Nếu có `WikiPageId`: dùng `wiki:{wikiPageId}`.
- Nếu không: dùng `SourceType:{SourceId}`.

Mỗi knowledge item tối đa 3 chunks.

Mục tiêu:

- Không để một document/wiki quá dài chiếm toàn bộ context.
- Cho AI thấy nhiều nguồn liên quan hơn.
- Giảm nguy cơ prompt quá dài.

### 20.3 Final context

Kết quả cuối cùng là danh sách chunks được chọn cho prompt.

Tối đa:

```text
8 chunks
```

Nếu không có chunk nào sau lọc/rerank, answer service sẽ trả câu trả lời yêu cầu user hỏi rõ hơn hoặc chọn phạm vi khác.

## 21. Gọi AnswerGenerationService

Service gọi:

```text
answerGenerationService.GenerateAsync(question, chunks)
```

Có hai implementation:

- `MockAnswerGenerationService`
- `OpenAiCompatibleAnswerGenerationService`

## 22. Mock answer generation

Mock service dùng cho local/test.

Nếu:

- Không có chunks.
- Hoặc không có keyword overlap giữa question và chunks.

Mock trả:

```text
NeedsClarification = true
Confidence = low
MissingInformation = ["Khong co nguon phu hop trong context da truy xuat."]
```

Nếu có chunks phù hợp:

- Lấy tối đa 3 chunks.
- Tóm tắt bằng cách ghép excerpt.
- Confidence là `medium` nếu có từ 2 chunks trở lên, ngược lại `low`.
- Cited source ids là source id của tối đa 3 chunks đầu.

Mock không phải câu trả lời AI thật, nhưng giúp test luồng RAG end-to-end.

## 23. OpenAI-compatible answer generation

Nếu dùng provider thật, service xây prompt có system prompt và user prompt.

### 23.1 System prompt

System prompt yêu cầu:

- Trả lời bằng tiếng Việt.
- Chỉ dùng sources được cung cấp.
- Không bịa facts, policy, price, date, procedure.
- Nếu nguồn không đủ hoặc mơ hồ, nói thiếu gì và hỏi lại ngắn gọn.
- Không mention nguồn ngoài prompt.
- Trả JSON đúng schema.

Schema yêu cầu:

```json
{
  "answer": "string",
  "confidence": "high|medium|low",
  "needsClarification": true,
  "clarifyingQuestion": "string|null",
  "citations": [{ "sourceId": "S1" }],
  "missingInformation": ["string"],
  "conflicts": ["string"],
  "suggestedFollowUps": ["string"]
}
```

### 23.2 User prompt

User prompt chứa:

1. Câu hỏi.
2. Danh sách sources.

Mỗi source được format:

```text
[S1]
SourceId: S1
Type: Wiki
Title: ...
Folder: ...
Section: ...
Text:
...
```

Mỗi chunk text bị trim tối đa:

```text
MaxChunkCharacters = 1600
```

Điều này giúp prompt không quá dài.

### 23.3 AI chỉ được cite source label trong prompt

Trong prompt, AI chỉ biết các source label:

```text
S1, S2, S3, ...
```

AI phải trả citations dạng:

```json
{
  "sourceId": "S1"
}
```

Backend sau đó map `S1` về source id thật của chunk.

### 23.4 Parse JSON answer

Backend cố parse JSON object từ output.

Nếu parse thành công:

- Lấy `answer`.
- Normalize confidence.
- Lấy `needsClarification`.
- Clean missing information/conflicts/follow-ups.
- Map citations S1/S2 về source id thật.

Nếu không cite nguồn nào mà `needsClarification = false`, backend hạ confidence về `low`.

Nếu confidence là low và có missing information, backend set `NeedsClarification = true`.

### 23.5 Repair nếu AI trả JSON lỗi

Nếu output đầu tiên không parse được:

1. Backend tạo repair prompt.
2. Gửi lại raw output lỗi và prompt gốc.
3. Yêu cầu provider trả JSON hợp lệ.

Nếu repair thành công, dùng kết quả repaired.

Nếu vẫn lỗi, backend fallback:

- Answer là raw answer nếu có.
- `NeedsClarification = true`.
- `Confidence = low`.
- `MissingInformation = ["Provider AI khong tra ve JSON dung schema."]`
- Suggested follow-up yêu cầu hỏi lại hoặc kiểm tra cấu hình provider.

## 24. Map citations về chunks thật

Answer draft trả về:

```text
CitedSourceIds
```

Những id này là source id thật đã được map từ `S1`, `S2`, ...

Backend tạo set:

```text
citedSourceIdSet
```

Nếu AI không cite source nào:

- Backend dùng toàn bộ final context chunks làm citations.

Nếu AI có cite:

- Backend chỉ lấy chunks có `SourceId` nằm trong cited source ids.

Sau đó citations được trả cho frontend bằng:

```text
AiCitationResponse
```

Mỗi citation excerpt được tạo bởi:

```text
ToExcerpt(chunk.Text)
```

Excerpt:

- Gộp whitespace.
- Tối đa 320 ký tự.

## 25. Lưu lịch sử hỏi đáp vào SQLite

Sau khi có answer draft và cited chunks, backend lưu interaction.

### 25.1 Tạo AiInteractionEntity

Backend tạo:

```text
AiInteractionEntity
```

Các trường chính:

- `Id`: interaction id mới.
- `UserId`: user hỏi.
- `Question`: câu hỏi đã trim.
- `Answer`: answer cuối cùng.
- `ScopeType`: All/Folder/Document.
- `ScopeFolderId`: folder scope nếu có.
- `ScopeDocumentId`: document scope nếu có.
- `NeedsClarification`: từ answer draft.
- `Confidence`: high/medium/low.
- `MissingInformationJson`: JSON array.
- `ConflictsJson`: JSON array.
- `SuggestedFollowUpsJson`: JSON array.
- `LatencyMs`: thời gian từ lúc bắt đầu embedding/retrieval/generation đến khi xong.
- `UsedWikiCount`: số cited chunks thuộc wiki.
- `UsedDocumentCount`: số cited chunks thuộc document.
- `CreatedAt`: thời điểm lưu.

Interaction dùng cho:

- Feedback sau câu trả lời.
- Dashboard KPI.
- Review câu trả lời sai.
- Truy vết nguồn đã dùng.

### 25.2 Tạo AiInteractionSourceEntity

Với mỗi cited chunk, backend tạo:

```text
AiInteractionSourceEntity
```

Các trường:

- `Id`: GUID mới.
- `AiInteractionId`: interaction id.
- `SourceType`: Document/Wiki/Correction.
- `SourceId`: source id.
- `DocumentId`: nếu có.
- `DocumentVersionId`: nếu có.
- `WikiPageId`: nếu có.
- `Title`: title nguồn.
- `FolderPath`: folder path.
- `SectionTitle`: section title nếu có.
- `Excerpt`: excerpt tối đa 320 ký tự.
- `Rank`: thứ tự citation.
- `CreatedAt`: thời điểm lưu.

Những source này là bằng chứng trả lời đã dùng nguồn nào tại thời điểm user hỏi.

### 25.3 SaveChanges

Backend gọi:

```text
dbContext.SaveChangesAsync()
```

Sau bước này:

- Câu hỏi đã được lưu.
- Câu trả lời đã được lưu.
- Sources/citations đã được lưu.
- User có thể submit feedback bằng `interactionId`.

## 26. Trả response về frontend

Backend trả:

```text
AskQuestionResponse
```

Gồm:

- `InteractionId`
- `Answer`
- `NeedsClarification`
- `Confidence`
- `MissingInformation`
- `Conflicts`
- `SuggestedFollowUps`
- `Citations`

Frontend hiển thị:

- Câu trả lời.
- Nguồn tham khảo.
- Trạng thái cần hỏi lại nếu có.
- Nút feedback đúng/sai.

## 27. Luồng sequence đầy đủ

```text
User
  |
  | POST /api/ai/ask
  v
AiController
  |
  | read user id from JWT
  | validate token
  v
AiQuestionService
  |
  | trim question
  | validate question not empty
  | validate scope
  | check folder/document permission
  v
FolderPermissionService
  |
  | get visible folder ids
  v
AiQuestionService
  |
  | normalize question
  | extract keywords
  | create embedding for question
  | build KnowledgeQueryFilter
  v
EmbeddingService
  |
  | create query vector
  v
Chroma Vector DB
  |
  | semantic search top 50
  | filter by metadata
  v
SQLite Keyword Index
  |
  | keyword search top 20
  | filter by metadata
  v
AiQuestionService
  |
  | merge vector + keyword candidates
  | parse metadata to chunks
  | filter requested scope
  | filter visible folders
  | query SQLite to verify document/wiki/correction validity
  | rerank chunks
  | pack max 8 context chunks
  v
AnswerGenerationService
  |
  | build prompt with question + sources S1..S8
  | call AI provider
  | parse JSON answer
  | repair if invalid JSON
  | fallback if still invalid
  v
AiQuestionService
  |
  | map cited S1/S2 to real source ids
  | build citation excerpts
  | save AiInteraction
  | save AiInteractionSources
  v
SQLite
  |
  | interaction history saved
  v
AiController
  |
  | return AskQuestionResponse
  v
Frontend
  |
  | show answer + citations + feedback controls
```

## 28. Explain retrieval endpoint

Ngoài endpoint hỏi thật, hệ thống có endpoint explain:

```http
POST /api/ai/retrieval/explain
```

Chỉ role sau được gọi:

- `Admin`
- `Reviewer`

Endpoint này không sinh answer. Nó chạy retrieval pipeline để giải thích:

- Query được normalize thế nào.
- Keywords là gì.
- Filter gồm những gì.
- Có bao nhiêu vector candidates.
- Có bao nhiêu keyword candidates.
- Có bao nhiêu candidates sau merge.
- Có bao nhiêu candidates qua permission filter.
- Final context gồm những chunks nào.
- Mỗi candidate bị reject hay được chọn vì lý do gì.

Response gồm:

- `Question`
- `ScopeType`
- `QueryUnderstanding`
- `Filter`
- `CandidateStats`
- `FinalContext`
- `Candidates`

Endpoint này hữu ích cho reviewer/admin khi debug:

- Vì sao AI không tìm thấy nguồn?
- Vì sao document đúng không được chọn?
- Vì sao wiki được ưu tiên?
- Chunk nào bị loại vì quyền?
- Chunk nào được chọn vào prompt?

## 29. Các tình huống trả lời không chắc hoặc cần hỏi lại

Hệ thống có thể trả `NeedsClarification = true` trong các trường hợp:

### 29.1 Không có chunk phù hợp

Không có source nào sau retrieval và permission filter.

Kết quả thường:

- Confidence low.
- Missing information nói không có nguồn phù hợp.
- Suggested follow-up đề nghị hỏi cụ thể hơn hoặc chọn folder/document khác.

### 29.2 Có chunk nhưng không đủ thông tin

AI provider thấy nguồn không trả lời chắc được.

Prompt yêu cầu:

- Không bịa.
- Nói thiếu gì.
- Hỏi lại ngắn gọn.

### 29.3 Nguồn mâu thuẫn

Nếu sources mâu thuẫn, AI có thể trả:

- `Conflicts`
- Confidence low/medium.
- Needs clarification true nếu cần user chọn phạm vi hoặc nguồn cụ thể hơn.

### 29.4 Provider trả output không đúng schema

Backend repair một lần.

Nếu vẫn lỗi:

- Confidence low.
- Needs clarification true.
- Missing information ghi provider không trả JSON đúng schema.

## 30. Cách permission được bảo vệ nhiều lớp

Luồng Q&A không chỉ dựa vào vector DB filter.

Các lớp bảo vệ:

1. Validate scope trước retrieval.
2. Lấy visible folder ids từ SQLite.
3. Build metadata filter cho vector DB.
4. Build filter cho keyword index.
5. Parse metadata và loại chunk ngoài requested scope.
6. Loại chunk ngoài visible folder.
7. Query SQLite để xác nhận document/wiki/correction còn hợp lệ.
8. Chỉ đưa chunks đã qua filter vào prompt.

Điều này giảm rủi ro lộ thông tin do:

- Vector DB còn record cũ.
- Metadata lỗi.
- Document version cũ chưa xóa khỏi vector DB.
- Wiki đã archived.
- User không còn quyền folder.

## 31. Tại sao wiki thường được ưu tiên hơn document?

Trong rerank, wiki có source priority cao hơn document:

```text
Wiki +45
Document +20
```

Lý do:

- Wiki đã được AI tổng hợp.
- Reviewer đã publish.
- Nội dung thường ngắn, rõ, chuẩn hóa hơn document gốc.
- Wiki có thể đại diện cho tri thức đã duyệt, còn document là nguồn gốc/fallback.

Tuy nhiên document vẫn có thể được chọn nếu:

- Match keyword tốt hơn.
- Match phrase tốt hơn.
- Đúng scope hơn.
- Wiki không có thông tin đủ rõ.

## 32. Vai trò của correction

Correction có priority cao nhất:

```text
Correction +100
```

Mục đích:

- Nếu reviewer đã tạo tri thức sửa lỗi/đính chính, hệ thống nên ưu tiên nó.
- Correction giúp cải thiện các câu trả lời từng bị feedback sai.

Correction chỉ được dùng khi:

- Status là Approved.
- Visibility hợp lệ.
- Nằm trong scope user hỏi.
- User có quyền folder nếu correction theo folder.

## 33. Dữ liệu nào được đọc trong Q&A?

Q&A đọc từ:

- `Documents`
- `DocumentVersions`
- `WikiPages`
- `KnowledgeCorrections`
- `KnowledgeChunkIndexes`
- Folder permissions.
- Chroma vector DB.
- AI provider settings.

Q&A không đọc file gốc khi trả lời.

Nó dùng chunks đã index sẵn trong:

- Vector DB.
- Keyword index.
- Metadata trong SQLite.

## 34. Dữ liệu nào được ghi trong Q&A?

Sau mỗi câu hỏi, hệ thống ghi:

- `AiInteractions`
- `AiInteractionSources`

Không ghi thêm document/wiki chunks trong luồng hỏi.

Nếu user gửi feedback sau đó, luồng feedback sẽ ghi thêm:

- `AiFeedback`
- Có thể tạo quality issue/correction workflow tùy module feedback.

## 35. Ranh giới trách nhiệm giữa backend và AI provider

Backend chịu trách nhiệm:

- Auth.
- Permission.
- Scope validation.
- Retrieval.
- Metadata filter.
- SQLite validation.
- Rerank.
- Context packing.
- Prompt construction.
- JSON parsing.
- Citation mapping.
- Lưu interaction.

AI provider chịu trách nhiệm:

- Sinh câu trả lời từ context.
- Đánh giá confidence.
- Chỉ ra missing information.
- Chỉ ra conflicts nếu có.
- Đề xuất follow-up.
- Chọn citations trong danh sách source labels được cung cấp.

AI provider không được tự tìm thêm nguồn và không được quyết định quyền truy cập.

## 36. Ví dụ minh họa

Giả sử user hỏi:

```text
Quy trình duyệt thanh toán như thế nào?
```

Scope:

```text
All
```

Luồng:

1. Backend validate question.
2. Backend lấy visible folders của user.
3. Backend normalize question thành:

```text
quy trinh duyet thanh toan nhu the nao
```

4. Keywords còn lại sau stop words có thể là:

```text
quy, trinh, duyet, thanh, toan
```

5. Backend tạo embedding cho câu hỏi.
6. Vector DB trả các chunks semantic gần câu hỏi.
7. Keyword index trả chunks có các từ `duyet`, `thanh`, `toan`.
8. Backend merge candidates.
9. Backend loại chunk ngoài quyền.
10. Backend xác nhận document version phải là current Indexed.
11. Backend xác nhận wiki page chưa archived và visible.
12. Backend rerank:
    - Wiki "Quy trình thanh toán" được +45.
    - Document "SOP thanh toán" được +20.
    - Chunk nào match keywords được cộng thêm.
13. Backend chọn tối đa 8 chunks.
14. Backend gửi prompt cho AI:

```text
Question:
Quy trình duyệt thanh toán như thế nào?

Sources:
[S1] Wiki ...
[S2] Document ...
```

15. AI trả JSON:

```json
{
  "answer": "Theo nguồn hiện có, quy trình gồm...",
  "confidence": "medium",
  "needsClarification": false,
  "citations": [{ "sourceId": "S1" }],
  "missingInformation": [],
  "conflicts": [],
  "suggestedFollowUps": ["Bạn có muốn xem điều kiện phê duyệt theo hạn mức không?"]
}
```

16. Backend map `S1` về wiki page thật.
17. Backend lưu interaction và source.
18. Frontend hiển thị answer và citation.

## 37. Những điểm cần nhớ

- User hỏi không làm hệ thống đọc toàn bộ file gốc.
- Câu trả lời dựa trên chunks đã index từ document/wiki/correction.
- Vector DB tìm semantic candidates, keyword index bổ sung từ khóa.
- SQLite vẫn là nguồn sự thật cho quyền và trạng thái nguồn.
- Wiki published được ưu tiên hơn document approved.
- Document chỉ hợp lệ nếu là current version và status `Indexed`.
- Answer service bắt AI trả JSON để backend kiểm soát confidence, citations và missing information.
- Mỗi lượt hỏi được lưu lại để phục vụ feedback, review và KPI.
