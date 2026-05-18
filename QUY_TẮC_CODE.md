# Internal Knowledge Copilot - Quy tắc code

Ngày lập: 2026-05-09

## 1. Nguyên tắc chung

- Code từng bước nhỏ, dễ review.
- Không thêm tính năng ngoài tài liệu MVP nếu chưa được xác nhận.
- Ưu tiên code rõ ràng hơn abstraction phức tạp.
- Tên biến/hàm/class dùng tiếng Anh theo chuẩn kỹ thuật.
- Text hiển thị cho người dùng dùng tiếng Việt.
- Mọi chú thích/comment trong code phải dùng tiếng Việt.

## 2. Quy tắc chú thích

Bắt buộc:

- Comment trong code dùng tiếng Việt có dấu.
- Nếu cần giải thích nghiệp vụ, viết ngắn gọn và trực tiếp.

Không nên:

- Không comment lại điều code đã quá rõ.
- Không dùng tiếng Anh trong comment.
- Không viết comment dài nếu có thể tách hàm rõ nghĩa.

Ví dụ đúng:

```csharp
// Chỉ tài liệu đã duyệt mới được đưa vào hàng đợi indexing.
```

Ví dụ không dùng:

```csharp
// Only approved documents can be indexed.
```

## 3. Backend

Stack:

- ASP.NET Core.
- SQLite.
- Qdrant.

Gợi ý cấu trúc:

```text
src/
  backend/
    InternalKnowledgeCopilot.Api/
      Controllers/
      Modules/
        Auth/
        Users/
        Folders/
        Documents/
        Wiki/
        Ai/
        Dashboard/
        Audit/
      Infrastructure/
        Database/
        FileStorage/
        Qdrant/
        AiProvider/
        BackgroundJobs/
      Common/
```

Quy tắc:

- Controller mỏng, logic nằm trong service/use case.
- Kiểm tra quyền ở service hoặc policy rõ ràng.
- Không cho download file trực tiếp từ web root.
- Không lưu API key trong source code.
- Không log secrets/password/API key.
- Không dùng đường dẫn file từ user input nếu chưa sanitize.

## 4. Frontend

Stack:

- Vue.

Gợi ý cấu trúc:

```text
src/
  frontend/
    src/
      api/
      components/
      layouts/
      pages/
        auth/
        documents/
        ai/
        wiki/
        review/
        admin/
        dashboard/
      stores/
      router/
```

Quy tắc:

- UI text tiếng Việt.
- Role-based navigation rõ ràng.
- Không hiển thị menu user không có quyền.
- Form phải có validation cơ bản.
- Empty/error/loading states phải rõ.

## 5. Database

Quy tắc:

- Dùng migration.
- Có created_at/updated_at cho bảng chính.
- Dữ liệu quan trọng dùng soft delete.
- Status dùng enum/string nhất quán.
- Không xóa hard file/tài liệu trong MVP trừ khi được xác nhận.

## 6. Qdrant và RAG

Quy tắc:

- Chỉ index document approved.
- Chỉ index wiki published.
- Payload phải có source_type.
- SQLite là nguồn sự thật cho quyền.
- Trước khi đưa chunk vào prompt, backend cần đảm bảo chunk thuộc phạm vi user được phép.
- Không để AI tự tạo citation ngoài nguồn retrieved.

## 7. AI prompt

Quy tắc:

- Prompt Q&A yêu cầu trả lời tiếng Việt.
- Nếu không đủ context, AI phải hỏi lại.
- Không cho AI bịa nguồn.
- Prompt sinh wiki không được thêm thông tin ngoài tài liệu nguồn.
- Prompt sinh wiki giữ ngôn ngữ theo tài liệu nguồn.

## 8. Logging

Quy tắc:

- Technical log ghi file.
- Audit log nghiệp vụ ghi SQLite.
- Không log nội dung nhạy cảm quá mức.
- Không log password/API key.

## 9. Testing

Ưu tiên test cho:

- Permission service.
- Document status/versioning.
- File upload validation.
- Qdrant filter theo quyền.
- AI feedback metrics.
- Wiki publish visibility.

MVP chưa cần test quá rộng cho UI, nhưng cần smoke test luồng chính.

## 10. Definition of Done

Một task được xem là xong khi:

- Code chạy được local.
- Không phá luồng đã có.
- Có kiểm tra quyền phù hợp.
- Có xử lý lỗi cơ bản.
- Có log/audit nếu là hành động nghiệp vụ chính.
- Comment nếu có đều dùng tiếng Việt.
- UI text nếu có dùng tiếng Việt.
- Tài liệu liên quan được cập nhật nếu thay đổi hành vi.
