# Internal Knowledge Copilot - UI Flow MVP

Ngày lập: 2026-05-09

## 1. Nguyên tắc UI

- Giao diện nội bộ, rõ ràng, ít trang trí.
- Ưu tiên thao tác nhanh và trạng thái dễ hiểu.
- Text hiển thị bằng tiếng Việt.
- Không cần landing page.
- Không cần mobile app trong MVP.

## 2. Layout chính

Các vùng chính:

- Sidebar navigation.
- Header user/menu.
- Main content.

Menu theo role:

- Dashboard
- Tài liệu
- Hỏi AI
- Wiki
- Review
- Quản trị

## 3. Admin flow

Admin cần làm:

- Tạo user.
- Gán role/team.
- Quản lý team.
- Quản lý folder.
- Gán quyền folder.
- Xem dashboard.
- Xem audit log.

Màn hình:

- Dashboard.
- User management.
- Team management.
- Folder management.
- Audit log.

## 4. User flow

User cần làm:

- Xem folder/tài liệu có quyền.
- Upload tài liệu.
- Xem trạng thái tài liệu đã upload.
- Xem lý do reject.
- Upload lại/version mới.
- Tải file gốc.
- Hỏi AI.
- Feedback đúng/sai.

Màn hình:

- Tài liệu của tôi.
- Cây thư mục.
- Upload tài liệu.
- Hỏi AI.

## 5. Reviewer flow

Reviewer cần làm:

- Duyệt/reject tài liệu.
- Tạo/sửa/xóa mềm folder.
- Gán quyền folder.
- Sinh wiki draft.
- Publish/reject wiki.
- Xem feedback sai.
- Xem dashboard.

Màn hình:

- Review tài liệu.
- Wiki drafts.
- Feedback sai.
- Dashboard.
- Folder management.

## 6. Màn hình Login

Chức năng:

- Nhập email/password.
- Hiển thị lỗi đăng nhập.
- Nếu must_change_password = true, chuyển sang màn đổi mật khẩu.

## 7. Màn hình Tài liệu

Chức năng:

- Hiển thị cây thư mục user có quyền.
- Danh sách tài liệu theo folder.
- Filter theo trạng thái.
- Upload tài liệu.
- Download file gốc.

Thông tin tài liệu:

- Tên.
- Folder.
- Status.
- Version hiện tại.
- Người upload.
- Ngày upload.
- Trạng thái indexing.

## 8. Màn hình Upload tài liệu

Fields:

- Folder.
- Title.
- Description.
- File.

Validate:

- Chỉ PDF/DOCX/Markdown/TXT.
- Tối đa 20MB.

Sau upload:

- Hiển thị trạng thái chờ duyệt.

## 9. Màn hình Review tài liệu

Chức năng:

- Danh sách tài liệu PendingReview.
- Xem metadata.
- Download file để kiểm tra.
- Approve.
- Reject và nhập lý do.

## 10. Màn hình Hỏi AI

Chức năng:

- Nhập câu hỏi.
- Chọn scope:
  - Tất cả tài liệu được phép.
  - Folder.
  - Document.
- Gửi câu hỏi.
- Hiển thị câu trả lời.
- Hiển thị nguồn.
- Feedback đúng/sai và ghi chú.

Nguồn hiển thị:

- Loại nguồn: Wiki hoặc Document.
- Tên.
- Đường dẫn/thư mục.
- Đoạn trích.

## 11. Màn hình Wiki Drafts

Chức năng:

- Danh sách wiki draft.
- Xem nội dung draft.
- Publish.
- Reject và nhập lý do.

Publish options:

- Theo folder/quyền nguồn.
- Public toàn công ty.

Nếu public toàn công ty:

- Bắt buộc checkbox xác nhận nội dung được phép public nội bộ.

## 12. Màn hình Feedback sai

Chức năng:

- Danh sách feedback Incorrect.
- Xem câu hỏi.
- Xem câu trả lời AI.
- Xem nguồn đã dùng.
- Xem ghi chú user.
- Cập nhật trạng thái xử lý.

Trạng thái:

- New.
- InReview.
- Resolved.

## 13. Dashboard

Chỉ Admin/Reviewer thấy.

Widgets MVP:

- Tài liệu theo trạng thái.
- Wiki theo trạng thái.
- Số lượt hỏi AI.
- Tỷ lệ feedback đúng/sai.
- Feedback sai chờ xử lý.
- Nguồn được trích dẫn nhiều.

Filter:

- Thời gian.
- Team.
- Folder.

## 14. Empty/error states

Cần có trạng thái rõ cho:

- Không có tài liệu.
- Không có quyền xem folder.
- Chưa có wiki draft.
- Chưa có feedback sai.
- AI không tìm thấy thông tin đủ chắc.
- Processing/indexing thất bại.

