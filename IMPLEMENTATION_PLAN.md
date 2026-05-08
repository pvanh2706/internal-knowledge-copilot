# Internal Knowledge Copilot - Kế hoạch triển khai MVP

Ngày lập: 2026-05-09

## 1. Nguyên tắc triển khai

- Code theo từng milestone nhỏ, mỗi milestone có thể review độc lập.
- Ưu tiên luồng nghiệp vụ chạy được trước, tối ưu sau.
- Không thêm tính năng ngoài phạm vi MVP nếu chưa được xác nhận.
- Backend, frontend, database và Qdrant cần có ranh giới rõ.
- Mọi chú thích/comment trong code phải dùng tiếng Việt.
- Text hiển thị trong UI ưu tiên tiếng Việt.

## 2. Phạm vi MVP cần hoàn thành

Luồng giá trị chính:

```text
User upload tài liệu
-> Reviewer duyệt
-> hệ thống extract/chunk/embed tài liệu vào Qdrant
-> User hỏi AI có nguồn
-> User feedback đúng/sai
-> Reviewer xem feedback sai
-> Reviewer sinh wiki draft từ tài liệu approved
-> Reviewer publish wiki
-> AI Q&A ưu tiên wiki published, fallback document approved
```

## 3. Milestone 0 - Khởi tạo project

Mục tiêu:

- Tạo cấu trúc project backend/frontend.
- Cấu hình chạy local.
- Cấu hình SQLite, local storage, Qdrant endpoint, AI provider settings.

Backend:

- ASP.NET Core API.
- Cấu trúc module rõ: Auth, Users, Folders, Documents, Wiki, Ai, Dashboard, Audit.
- SQLite migration ban đầu.
- File logging.

Frontend:

- Vue app.
- Router.
- Layout nội bộ đơn giản.
- Trang login placeholder.

Tiêu chí hoàn thành:

- Backend chạy được.
- Frontend chạy được.
- API health check hoạt động.
- SQLite database được tạo qua migration.
- Config đọc được từ appsettings/environment.

## 4. Milestone 1 - Auth, user, role, team

Mục tiêu:

- Có đăng nhập cơ bản.
- Admin quản lý user/team/role.

Backend:

- User model.
- Role: Admin, User, Reviewer.
- Team model.
- Password hashing.
- Login API.
- Change password API.
- Admin tạo user.
- Admin gán role/team.

Frontend:

- Login page.
- Đổi mật khẩu lần đầu.
- User management page cho Admin.

Tiêu chí hoàn thành:

- Admin tạo được user.
- User đăng nhập được.
- User đổi mật khẩu được.
- API kiểm tra role hoạt động.

## 5. Milestone 2 - Folder tree và phân quyền

Mục tiêu:

- Quản lý cây thư mục.
- Phân quyền theo team/folder.

Backend:

- Folder CRUD cho Admin/Reviewer.
- Folder permission theo team.
- Service kiểm tra user có quyền xem folder/document.

Frontend:

- Folder tree đơn giản.
- Trang quản lý folder cho Admin/Reviewer.
- UI gán quyền team vào folder.

Tiêu chí hoàn thành:

- Admin/Reviewer tạo/sửa/xóa mềm folder.
- User chỉ thấy folder có quyền.
- Permission service có test cơ bản.

## 6. Milestone 3 - Upload, review và versioning tài liệu

Mục tiêu:

- User upload tài liệu.
- Reviewer approve/reject.
- Versioning đơn giản.

Backend:

- Upload PDF/DOCX/Markdown/TXT.
- Lưu file vào local storage ngoài web root.
- Lưu metadata vào SQLite.
- Document status: PendingReview, Approved, Rejected, Archived.
- DocumentVersion status: PendingReview, Approved, Rejected.
- Reject reason.
- Upload version mới bằng cách chọn document hiện có.
- Version cũ vẫn current cho đến khi version mới approved.
- Download file qua API có kiểm tra quyền.

Frontend:

- Trang danh sách tài liệu theo trạng thái.
- Upload document.
- Upload version mới.
- Review queue cho Reviewer.
- Approve/reject document.
- Download file.

Tiêu chí hoàn thành:

- User upload được file hợp lệ.
- Reviewer duyệt/reject được.
- File rejected không vào index.
- File approved có trạng thái sẵn sàng xử lý.
- User có quyền tải được file gốc.

## 7. Milestone 4 - Document processing và Qdrant indexing

Mục tiêu:

- Extract text từ tài liệu approved.
- Chunk, embedding, lưu vào Qdrant.

Backend:

- Processing job table.
- Hosted service xử lý job.
- Text extraction:
  - TXT/Markdown: đọc text trực tiếp.
  - DOCX: parser dựa trên OpenXML hoặc thư viện .NET phù hợp.
  - PDF: thư viện .NET phù hợp.
- Chunking service.
- Embedding client.
- Qdrant client.
- Upsert chunks kèm metadata.
- Đánh dấu indexed.

Frontend:

- Hiển thị trạng thái xử lý/indexing của tài liệu.

Tiêu chí hoàn thành:

- Approve document tạo job xử lý.
- Tài liệu được extract/chunk/embed.
- Qdrant có chunks với payload đúng.
- Nếu processing lỗi, trạng thái lỗi hiển thị được.

## 8. Milestone 5 - AI Q&A có nguồn

Mục tiêu:

- User hỏi AI trên tài liệu/wiki có quyền.
- Trả lời tiếng Việt, có nguồn.

Backend:

- Q&A API.
- Scope: all, folder, document.
- Tạo embedding cho câu hỏi.
- Search Qdrant với permission filter.
- Ưu tiên wiki published.
- Fallback document approved.
- Prompt trả lời tiếng Việt.
- Nếu không đủ thông tin, hỏi lại người dùng.
- Lưu ai_interaction tối thiểu.

Frontend:

- Giao diện hỏi đáp đơn giản.
- Chọn scope.
- Hiển thị answer.
- Hiển thị citations: loại nguồn, tên, đường dẫn/thư mục, đoạn trích.

Tiêu chí hoàn thành:

- User hỏi được trong phạm vi có quyền.
- Câu trả lời có nguồn.
- Không truy xuất tài liệu user không có quyền.
- Wiki được ưu tiên nếu có dữ liệu phù hợp.

## 9. Milestone 6 - Feedback và reviewer queue

Mục tiêu:

- User feedback câu trả lời AI.
- Reviewer xử lý feedback sai.

Backend:

- Feedback API.
- Lưu đúng/sai, ghi chú, user, thời gian.
- Queue feedback sai.
- Trạng thái xử lý feedback: New, InReview, Resolved.

Frontend:

- Nút feedback đúng/sai dưới câu trả lời.
- Ghi chú feedback.
- Reviewer page xem feedback sai.
- Reviewer cập nhật trạng thái xử lý.

Tiêu chí hoàn thành:

- Feedback được lưu.
- Tỷ lệ đúng/sai tính được.
- Reviewer xem được danh sách câu trả lời sai.

## 10. Milestone 7 - Wiki draft và publish

Mục tiêu:

- Reviewer sinh wiki draft từ document approved.
- Publish wiki và index vào Qdrant.

Backend:

- Generate wiki draft API.
- Prompt sinh wiki theo ngôn ngữ tài liệu nguồn.
- Lưu wiki draft.
- Publish/reject wiki.
- Nếu publish toàn công ty, bắt buộc xác nhận public nội bộ.
- Chunk/embed wiki published vào Qdrant.

Frontend:

- Nút Generate wiki draft trên document approved.
- Trang danh sách wiki draft.
- Trang xem wiki draft.
- Publish/reject wiki.

Tiêu chí hoàn thành:

- Reviewer sinh được draft.
- Reviewer publish/reject được.
- Wiki published được index vào Qdrant.
- AI Q&A ưu tiên wiki published.

## 11. Milestone 8 - Dashboard KPI và audit log

Mục tiêu:

- Admin/Reviewer xem dashboard đơn giản.
- Audit log các hành động chính.

Backend:

- Audit log service.
- Dashboard API.
- Metrics:
  - tài liệu theo trạng thái
  - wiki theo trạng thái
  - lượt hỏi AI
  - tỷ lệ feedback đúng/sai
  - feedback sai chờ xử lý
  - nguồn được dùng nhiều

Frontend:

- Dashboard page.
- Bộ lọc đơn giản theo thời gian/team/folder nếu dữ liệu đủ.

Tiêu chí hoàn thành:

- Admin/Reviewer xem dashboard được.
- Hành động chính có audit log.

## 12. Milestone 9 - Hardening MVP

Mục tiêu:

- Kiểm tra lại bảo mật, quyền, lỗi xử lý file, backup.

Công việc:

- Kiểm tra quyền API.
- Kiểm tra file download không bypass API.
- Kiểm tra path traversal khi upload/download.
- Kiểm tra giới hạn 20MB/file.
- Kiểm tra Qdrant filter không trả dữ liệu ngoài quyền.
- Kiểm tra backup SQLite + storage folder.
- Viết hướng dẫn chạy local và deploy IIS.

Tiêu chí hoàn thành:

- Có checklist bảo mật MVP.
- Có hướng dẫn chạy.
- Có test smoke cho luồng chính.

## 13. Thứ tự ưu tiên nếu cần cắt scope

Nếu thời gian bị giới hạn, giữ lại:

1. Auth/user/role.
2. Folder permission.
3. Upload/review tài liệu.
4. Document processing + Qdrant.
5. AI Q&A có nguồn.
6. Feedback đúng/sai.

Có thể lùi sang sau:

- Dashboard chi tiết.
- Review queue nâng cao.
- Wiki public toàn công ty.
- Upload version mới nâng cao.
- Bộ lọc dashboard theo nhiều chiều.

Không nên lùi khỏi MVP nếu mục tiêu vẫn là chuẩn hóa tri thức:

- Generate wiki draft.
- Publish wiki.
- Q&A ưu tiên wiki published.

