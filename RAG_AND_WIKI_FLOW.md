# Internal Knowledge Copilot - RAG và Wiki Flow

Ngày lập: 2026-05-09

## 1. Mục tiêu

Tài liệu này mô tả cách hệ thống:

- Trích xuất nội dung từ tài liệu.
- Chia chunk.
- Tạo embeddings.
- Lưu vào Qdrant.
- Trả lời AI có nguồn.
- Sinh wiki draft.
- Ưu tiên wiki published khi Q&A.

## 2. Nguồn tri thức

Có 2 loại nguồn:

- Document: tài liệu gốc đã approved.
- Wiki: tri thức đã được AI tổng hợp và Reviewer publish.

Vai trò:

- Document dùng để kiểm chứng và fallback.
- Wiki là lớp tri thức chuẩn hóa, được ưu tiên khi trả lời.

## 3. Luồng indexing document

```text
Document version approved
-> tạo processing job
-> extract text
-> chunk text
-> tạo embedding cho từng chunk
-> upsert vào Qdrant với source_type = document
-> đánh dấu version indexed
```

Chỉ index:

- document version approved.
- current version hoặc version đang được hệ thống cho phép dùng.

Không index:

- pending review.
- rejected.
- deleted.

## 4. Luồng indexing wiki

```text
Wiki draft published
-> chunk wiki content
-> tạo embedding cho từng chunk
-> upsert vào Qdrant với source_type = wiki
-> wiki sẵn sàng làm nguồn ưu tiên cho AI Q&A
```

Chỉ index:

- wiki published.

Không index:

- draft.
- rejected.
- archived.

## 5. Chunking strategy MVP

Khuyến nghị ban đầu:

- Chunk khoảng 500-800 tokens.
- Overlap khoảng 80-120 tokens.
- Giữ metadata title, folder_path, document_version_id.
- Không split giữa heading lớn nếu có thể.

Với Markdown:

- Ưu tiên split theo heading.

Với TXT:

- Split theo đoạn rồi gom đến kích thước chunk mục tiêu.

Với DOCX/PDF:

- Extract text rồi split theo đoạn.
- MVP chưa cần xử lý bảng/hình ảnh nâng cao.

## 6. Qdrant payload

Payload tối thiểu:

```json
{
  "chunk_id": "chunk-id",
  "source_type": "document",
  "source_id": "document-version-id",
  "document_id": "document-id",
  "document_version_id": "version-id",
  "wiki_page_id": null,
  "folder_id": "folder-id",
  "team_id": "team-id",
  "title": "Tên tài liệu",
  "folder_path": "/Support/Payment",
  "version_number": 3,
  "status": "approved",
  "visibility_scope": "folder",
  "chunk_text": "Nội dung chunk..."
}
```

Với wiki:

```json
{
  "source_type": "wiki",
  "wiki_page_id": "wiki-id",
  "visibility_scope": "company",
  "status": "published"
}
```

## 7. Permission filtering

Nguyên tắc:

- SQLite là nguồn sự thật cho quyền.
- Qdrant filter chỉ giúp giảm tập kết quả.
- Backend có thể kiểm tra lại quyền trước khi đưa chunk vào prompt.

Luồng:

```text
User hỏi
-> lấy danh sách folder/document/wiki được phép từ SQLite
-> build Qdrant filter
-> search vector
-> kiểm tra lại quyền nếu cần
-> đưa chunks hợp lệ vào prompt
```

## 8. Retrieval strategy

Thứ tự retrieval:

1. Search wiki published trước.
2. Nếu đủ kết quả liên quan, dùng wiki làm nguồn chính.
3. Nếu chưa đủ, search document approved.
4. Gộp và rerank đơn giản theo score.
5. Nếu điểm liên quan quá thấp, hỏi lại người dùng để làm rõ.

MVP chưa cần reranker riêng.

## 9. Prompt Q&A

Yêu cầu prompt:

- Trả lời bằng tiếng Việt.
- Chỉ dùng thông tin từ context.
- Nếu context không đủ, hỏi lại người dùng để làm rõ.
- Không bịa nguồn.
- Nêu nguồn theo citations do backend cung cấp.
- Nếu nguồn tiếng Anh, được phép dịch/tóm tắt sang tiếng Việt.

Prompt skeleton:

```text
Bạn là trợ lý tri thức nội bộ.
Hãy trả lời bằng tiếng Việt.
Chỉ sử dụng thông tin trong phần NGỮ CẢNH.
Nếu NGỮ CẢNH không đủ để trả lời chắc chắn, hãy hỏi lại người dùng để làm rõ.
Không tự suy đoán và không tạo nguồn không có trong NGỮ CẢNH.

NGỮ CẢNH:
{context_chunks}

CÂU HỎI:
{question}
```

## 10. Prompt sinh wiki draft

Mục tiêu:

- Biến một tài liệu approved thành wiki draft ngắn gọn, có cấu trúc.
- Ngôn ngữ wiki theo ngôn ngữ tài liệu nguồn.
- Không thêm thông tin ngoài tài liệu.

Template draft đề xuất:

```text
# {Tên wiki}

## Mục đích

## Phạm vi áp dụng

## Nội dung chính

## Quy trình / Các bước thực hiện

## Lưu ý quan trọng

## Câu hỏi thường gặp

## Nguồn
```

Prompt skeleton:

```text
Bạn là người hỗ trợ chuẩn hóa tri thức nội bộ.
Hãy tạo wiki draft từ tài liệu bên dưới.
Giữ ngôn ngữ theo ngôn ngữ chính của tài liệu nguồn.
Không thêm thông tin ngoài tài liệu.
Nếu tài liệu thiếu thông tin cho một mục, ghi "Chưa có thông tin trong tài liệu nguồn".

TÀI LIỆU NGUỒN:
{document_text}
```

## 11. Citation strategy

Mỗi câu trả lời cần trả về:

- source_type: Wiki hoặc Document.
- title.
- folder_path.
- excerpt.

Không cần click source trong MVP, nhưng backend vẫn nên giữ source_id để mở rộng sau.

## 12. Xử lý khi không đủ chắc

AI nên hỏi lại nếu:

- Không có chunk nào đủ liên quan.
- Chunks mâu thuẫn rõ ràng.
- Câu hỏi quá rộng hoặc thiếu ngữ cảnh.
- Scope người dùng chọn không có dữ liệu phù hợp.

Ví dụ phản hồi:

```text
Mình chưa tìm thấy thông tin đủ rõ trong tài liệu hiện có. Bạn muốn hỏi trong phạm vi sản phẩm, module hoặc quy trình nào?
```

