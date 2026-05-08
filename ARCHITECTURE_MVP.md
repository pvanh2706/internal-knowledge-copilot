# Internal Knowledge Copilot - Kiến trúc MVP

Ngày lập: 2026-05-09

## 1. Mục tiêu kiến trúc

MVP cần chứng minh giá trị cho quản lý tri thức nội bộ, nhưng vẫn đủ sạch để mở rộng nếu kết quả tốt.

Ưu tiên kiến trúc:

- Nhỏ, dễ chạy nội bộ.
- Phù hợp Windows Server/IIS.
- Dễ review từng bước.
- Dễ đo KPI.
- Có RAG để hỏi đáp tài liệu có nguồn.
- Có wiki để chuẩn hóa tri thức, không chỉ hỏi đáp trên tài liệu gốc.
- Không over-engineering ở MVP.

## 2. Stack đã chốt

Frontend:

- Vue.

Backend:

- ASP.NET Core.
- Deploy qua IIS trên Windows Server.

Metadata database:

- SQLite cho MVP.
- Schema nên thiết kế theo hướng dễ migrate sang SQL Server/PostgreSQL sau này.

Vector database:

- Qdrant local/internal service.
- Dùng để lưu embedding chunks của tài liệu và wiki.

File storage:

- Local folder/file system nội bộ.
- Lưu file gốc upload.

AI provider:

- Dùng AI API riêng nếu cần.
- Chỉ dùng provider có cam kết enterprise/privacy rõ ràng.
- ChatGPT Plus không được xem là API/enterprise setup cho ứng dụng nội bộ.

Document processing:

- Nằm trong .NET backend ở MVP.
- Chưa tách Python service.

Không dùng trong MVP:

- Elasticsearch.
- Redis.
- SQL Server.
- SSO.
- Email notification.

## 3. Kiến trúc tổng quan

```text
[Vue UI]
   |
   v
[ASP.NET Core API on IIS]
   |
   +--> [SQLite]
   |      - users / roles / teams
   |      - folders / permissions
   |      - documents / document_versions
   |      - wiki_drafts / wiki_pages
   |      - ai_interactions / ai_feedback
   |      - audit_logs
   |
   +--> [Local File Storage]
   |      - original uploaded files
   |
   +--> [Document Processing]
   |      - extract text
   |      - split chunks
   |      - create embeddings
   |
   +--> [Qdrant]
   |      - document chunks
   |      - wiki chunks
   |      - metadata filters
   |
   +--> [AI API]
          - embeddings
          - Q&A answer generation
          - wiki draft generation
```

## 4. Vai trò của wiki trong hệ thống

Wiki là phần cốt lõi của sản phẩm, không phải tính năng phụ.

RAG trên tài liệu gốc giúp người dùng hỏi đáp nhanh, nhưng nếu chỉ có RAG thì hệ thống vẫn phụ thuộc vào tài liệu rời rạc, dài, trùng lặp hoặc lỗi thời. Wiki là lớp tri thức đã được AI tổng hợp và Reviewer duyệt.

Luồng tri thức mong muốn:

```text
Tài liệu gốc
-> AI sinh wiki draft
-> Reviewer duyệt/publish
-> Wiki published
-> AI Q&A ưu tiên dùng wiki
-> Feedback giúp cải thiện tri thức
```

Vai trò của từng nguồn:

- Document: nguồn gốc, dùng để kiểm chứng, fallback khi wiki chưa đủ.
- Wiki: nguồn tri thức chuẩn hóa, đã qua reviewer, nên được ưu tiên khi trả lời.

## 5. Module chính

### 5.1 Auth/User/Role

Chức năng:

- Admin tạo user.
- Admin gán role.
- User đổi mật khẩu sau lần đăng nhập đầu.

Role:

- Admin.
- User.
- Reviewer.

### 5.2 Team/Folder/Permission

Chức năng:

- Quản lý team chính của user.
- Quản lý cây thư mục.
- Phân quyền theo team/folder.
- Cho phép override quyền ở document khi cần.

Quyết định MVP:

- User không có quyền thì không thấy folder/tài liệu đó.
- Cách này giảm rủi ro lộ thông tin qua tên thư mục/tài liệu.

### 5.3 Document/Version/Approval

Chức năng:

- User upload tài liệu.
- Reviewer approve/reject.
- Reject bắt buộc nhập lý do.
- Tài liệu approved mới được dùng cho AI Q&A và wiki.
- Hỗ trợ versioning đơn giản.

Versioning MVP:

- User chọn tài liệu cũ rồi upload version mới.
- Không tự đoán version theo tên file.
- Version cũ tiếp tục là bản hiện tại cho đến khi version mới được approved.
- Khi version mới approved, hệ thống index version mới và đánh dấu là current.

### 5.4 Document Processing

Chức năng:

- Extract text từ file approved.
- Split text thành chunks.
- Gọi AI embedding API.
- Lưu chunks vào Qdrant.

Định dạng MVP:

- PDF.
- DOCX.
- Markdown.
- TXT.

### 5.5 AI Q&A

Chức năng:

- User hỏi AI bằng tiếng Việt.
- Scope hỏi:
  - toàn bộ tài liệu/wiki user có quyền
  - một folder
  - một document
- AI trả lời bằng tiếng Việt.
- Nếu không đủ chắc, AI hỏi lại để làm rõ.
- Câu trả lời có nguồn.

Nguồn trả lời gồm:

- Loại nguồn: Wiki hoặc Document.
- Tên tài liệu/wiki.
- Đường dẫn/thư mục.
- Đoạn trích ngắn liên quan.

### 5.6 Wiki Draft/Publish

Chức năng:

- Reviewer chọn document approved.
- Reviewer bấm Generate wiki draft.
- AI sinh draft wiki từ document.
- Reviewer publish hoặc reject.
- Reject bắt buộc nhập lý do.
- Wiki published được index vào Qdrant.

Quyết định MVP:

- Sinh wiki từ từng document trước.
- Chưa sinh wiki từ nhiều document/folder.
- Chưa cần editor sửa nội dung wiki trong app.
- Chưa cần versioning wiki.
- Chưa cần browse/search wiki riêng.

### 5.7 Feedback

Chức năng:

- User feedback câu trả lời AI là đúng/sai.
- Có ghi chú.
- Feedback sai tạo danh sách cho Reviewer xử lý.

Nên lưu tối thiểu:

- Câu hỏi.
- Câu trả lời AI.
- Nguồn đã dùng.
- Feedback đúng/sai.
- Ghi chú.
- Thời gian.
- Người feedback.
- Trạng thái xử lý.

### 5.8 Dashboard KPI

Chức năng:

- Chỉ Admin và Reviewer xem.
- Dashboard đơn giản.

Chỉ số MVP:

- Số tài liệu theo trạng thái.
- Số wiki theo trạng thái.
- Số lượt hỏi AI.
- Tỷ lệ feedback đúng/sai.
- Số feedback sai chờ xử lý.
- Số tài liệu/wiki được dùng làm nguồn nhiều.

### 5.9 Audit Log

Ghi audit log cơ bản cho:

- Upload tài liệu.
- Approve/reject tài liệu.
- Generate wiki draft.
- Publish/reject wiki.
- Sửa quyền.
- Feedback AI.

## 6. Dữ liệu lưu ở đâu

### 6.1 SQLite

SQLite là nguồn sự thật cho dữ liệu nghiệp vụ:

- users
- roles
- teams
- user_team
- folders
- folder_permissions
- documents
- document_versions
- document_permissions
- wiki_drafts
- wiki_pages
- ai_interactions
- ai_feedback
- audit_logs
- processing_jobs

### 6.2 Local folder

Lưu file gốc upload:

```text
/storage
  /documents
    /{document_id}
      /{version_id}
        original-file.ext
```

File path thật lưu trong SQLite.

### 6.3 Qdrant

Qdrant lưu embedding chunks cho RAG.

Payload metadata nên có:

- chunk_id
- source_type: document hoặc wiki
- source_id
- document_id
- document_version_id
- wiki_page_id
- folder_id
- team_id
- title
- folder_path
- version_number
- status: approved hoặc published
- visibility_scope
- chunk_text

Qdrant không phải nguồn sự thật cho quyền. SQLite vẫn là nguồn sự thật. Qdrant payload dùng để filter nhanh khi retrieval.

## 7. Luồng upload và index tài liệu

```text
User upload file
-> API kiểm tra loại file và dung lượng
-> lưu file gốc vào local folder
-> lưu metadata vào SQLite với trạng thái PendingReview
-> ghi audit log
-> Reviewer approve/reject
```

Nếu reject:

```text
Reviewer nhập lý do
-> document version chuyển sang Rejected
-> uploader thấy lý do và upload lại
-> ghi audit log
```

Nếu approve:

```text
Document version chuyển sang Approved
-> tạo processing job
-> extract text
-> chunk text
-> tạo embeddings
-> upsert chunks vào Qdrant
-> đánh dấu version indexed
-> nếu là version mới, đánh dấu version này là current
-> ghi audit log
```

## 8. Luồng AI Q&A

```text
User đặt câu hỏi
-> chọn scope: all / folder / document
-> backend xác định quyền user từ SQLite
-> tạo embedding cho câu hỏi
-> search Qdrant trong wiki published trước
-> nếu wiki đủ liên quan, dùng wiki làm nguồn chính
-> nếu wiki không đủ, search thêm document approved
-> gọi AI API sinh câu trả lời tiếng Việt
-> trả answer + citations
-> lưu interaction tối thiểu để phục vụ feedback/KPI
```

Chiến lược nguồn:

1. Ưu tiên wiki published.
2. Fallback sang document approved.
3. Nếu cả hai không đủ, hỏi lại người dùng để làm rõ.

Lý do:

- Wiki là tri thức đã chuẩn hóa.
- Document là nguồn gốc để kiểm chứng hoặc bổ sung.
- Cách này giúp sản phẩm không chỉ là RAG trên tài liệu rời rạc.

## 9. Luồng feedback AI

```text
User nhận câu trả lời
-> đánh dấu đúng/sai
-> nhập ghi chú nếu cần
-> lưu feedback vào SQLite
-> nếu sai, đưa vào hàng chờ Reviewer xử lý
-> cập nhật dashboard KPI
```

Reviewer xử lý feedback sai:

```text
Reviewer xem câu hỏi/câu trả lời/nguồn/ghi chú
-> xác định nguyên nhân:
     - tài liệu thiếu
     - wiki thiếu
     - retrieval sai
     - câu trả lời AI sai
     - user hỏi chưa rõ
-> cập nhật trạng thái xử lý
```

## 10. Luồng sinh wiki draft

```text
Reviewer chọn document approved/current
-> bấm Generate wiki draft
-> backend lấy text hoặc chunks của document version hiện tại
-> gọi AI API sinh wiki draft theo template
-> lưu draft vào SQLite với trạng thái Draft
```

Reviewer publish:

```text
Reviewer kiểm tra draft
-> chọn phạm vi visibility:
     - theo thư mục/quyền nguồn
     - hoặc public toàn công ty
-> nếu public toàn công ty, bắt buộc xác nhận nội dung được phép public nội bộ
-> publish wiki
-> chunk wiki content
-> tạo embeddings
-> upsert vào Qdrant với source_type = wiki
-> ghi audit log
```

Reviewer reject:

```text
Reviewer nhập lý do reject
-> draft chuyển sang Rejected
-> ghi audit log
```

## 11. Quyền truy cập và RAG

Nguyên tắc:

- User chỉ hỏi được trên tài liệu họ có quyền.
- Wiki published có thể có visibility riêng.
- Wiki public toàn công ty chỉ được publish sau khi Reviewer xác nhận.
- SQLite là nơi kiểm tra quyền cuối cùng.

Flow filter Qdrant:

```text
User
-> lấy danh sách folder/document/wiki được phép từ SQLite
-> build Qdrant filter theo source_type, source_id, folder_id, visibility_scope
-> search vectors
-> trước khi dùng chunks để trả lời, backend có thể kiểm tra lại quyền bằng SQLite nếu cần
```

## 12. Background processing

MVP dùng background processing đơn giản trong .NET:

- Có bảng processing_jobs trong SQLite.
- Hosted service xử lý job.
- Job types:
  - extract_document
  - embed_document
  - generate_wiki_draft
  - embed_wiki

Chưa cần:

- Hangfire.
- RabbitMQ.
- Redis queue.
- Worker service riêng.

Khi scale hoặc job dài/phức tạp hơn, có thể tách worker service sau.

## 13. Search thông thường

MVP có thể bắt đầu với search đơn giản:

- Search metadata trong SQLite: title, folder, status.
- Search text đầy đủ có thể để sau nếu chưa cần.

Không dùng Elasticsearch trong MVP để giảm vận hành.

Nếu KPI cho thấy người dùng vẫn cần tìm tài liệu bằng keyword nhiều, v1.1 có thể thêm:

- SQLite FTS nếu vẫn nhỏ.
- Hoặc Elasticsearch nếu muốn tận dụng hạ tầng sẵn có.

## 14. Logging và audit

Technical log:

- Ghi file log trên server.

Audit log nghiệp vụ:

- Lưu trong SQLite.
- Dùng cho dashboard và truy vết hành động chính.

Không ghi audit đầy đủ cho:

- Mọi lượt xem tài liệu.
- Mọi lượt hỏi AI chi tiết ngoài bản ghi tối thiểu cần cho feedback/KPI.

## 15. Deploy MVP

Triển khai dự kiến:

- Vue build static files.
- ASP.NET Core API chạy sau IIS.
- SQLite file đặt trong thư mục app data có backup.
- Local storage folder đặt ngoài web root.
- Qdrant chạy local/internal service trên server nội bộ.
- API key AI provider lưu trong environment variable hoặc secret config của server.

Lưu ý:

- Không đặt file upload trong public web root.
- Download file phải đi qua API để kiểm tra quyền.
- Cần backup SQLite và storage folder cùng nhau.
- Cần backup Qdrant hoặc có khả năng rebuild index từ SQLite + file/text đã extract.

## 16. Ngoài phạm vi MVP

Không làm trong MVP:

- Import Confluence tự động.
- Excel/PowerPoint/HTML/ảnh.
- SSO.
- Email notification.
- Preview file trong app.
- Editor sửa wiki trong app.
- Sinh wiki từ nhiều tài liệu/folder.
- Versioning wiki.
- Elasticsearch.
- Redis.
- Python service riêng.
- Full audit cho xem tài liệu/hỏi AI.
- Secret scanning/masking/redaction.
- Virus/malware scanning.
- Codebase/API/workflow/business rule understanding.
- Mobile app.
- Export report.
- Fine-tuning.

## 17. Roadmap mở rộng

### v1.1

- Keyword/full-text search tốt hơn.
- Import Confluence.
- Preview PDF/text.
- Email hoặc in-app notification tốt hơn.
- Wiki editor cơ bản.
- Sinh wiki từ folder/chủ đề.
- Cải thiện dashboard KPI.

### v2

- SSO.
- SQL Server/PostgreSQL thay SQLite.
- Worker service riêng.
- Elasticsearch cho search nâng cao nếu cần.
- Redis/queue nếu workload tăng.
- Secret scanning/masking.
- Virus/malware scanning.
- Versioning wiki.
- Knowledge graph/business rule catalog.
- Hiểu codebase/API/workflow.

## 18. Quyết định đã chốt

- MVP dùng Vue + ASP.NET Core.
- Metadata dùng SQLite.
- Vector DB dùng Qdrant.
- File gốc lưu local folder.
- Qdrant chạy local/internal service.
- Document processing nằm trong .NET backend trước.
- Chưa dùng Elasticsearch/Redis.
- Chưa tách Python service.
- Wiki là lớp tri thức chuẩn hóa, không bỏ khỏi MVP.
- AI Q&A ưu tiên wiki published, fallback sang document approved.
- Sinh wiki MVP từ từng document approved.
- Tài liệu chưa approved không được dùng cho AI Q&A hoặc wiki.
- Version mới chưa approved không thay thế version hiện tại.
