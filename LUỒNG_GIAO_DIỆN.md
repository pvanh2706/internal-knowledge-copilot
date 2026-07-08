# Internal Knowledge Copilot - UI Flow

Ngày lập: 2026-05-09. Cập nhật: 2026-07-09 theo UI mới sau redesign (xem [docs/KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md](docs/KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md)).

## 1. Nguyên tắc UI

- Phong cách tối giản hiện đại kiểu Linear/Notion: nhiều khoảng trắng, border thay shadow, 1 màu nhấn Indigo.
- Light + Dark mode (3 chế độ Sáng/Tối/Theo hệ thống, đổi ở topbar).
- Text hiển thị tiếng Việt có dấu (kể cả aria-label, placeholder).
- Ưu tiên thao tác nhanh và trạng thái dễ hiểu: mọi trang có đủ loading (skeleton) / empty / error state; thao tác báo bằng toast; hành động phá hủy luôn có hộp xác nhận.
- On-prem: font self-host, không gọi CDN.
- Không cần landing page, không cần mobile app.

## 2. Layout

2 layout, chọn theo `meta.layout` của route:

- **AuthLayout** — khối hẹp căn giữa cho Đăng nhập / Đổi mật khẩu.
- **AppLayout** — mọi trang còn lại:
  - Sidebar trái cố định 256px, menu config-driven (`components/app/navigation.ts`) chia 4 nhóm: *Làm việc*, *Chất lượng* (Reviewer+), *Quản trị* (Admin), *Nền tảng*. Quyền khai báo theo từng item, khớp meta router.
  - Topbar: tên trang hiện tại, badge tenant, nút đổi giao diện sáng/tối, menu user (tên/vai trò + Đăng xuất).
  - Banner cảnh báo mỏng dưới topbar khi LLM chưa cấu hình API key (Admin có link "Cấu hình ngay").
  - Content padding 24px; trang quản trị bó `max-w-6xl`; Hỏi AI dùng cột đơn `max-w-3xl` căn giữa.

Menu theo role:

- Làm việc: Tổng quan, Tài liệu, Hỏi AI, Wiki (Reviewer+)
- Chất lượng (Reviewer+): Duyệt tài liệu, Phản hồi, Đánh giá, Giải thích truy xuất, Chỉ mục tri thức, Nguồn tri thức, Thư mục
- Quản trị (Admin): Người dùng, Team, Cấu hình AI, Nhật ký hệ thống, Reset dữ liệu
- Nền tảng: Gợi ý quy trình, Hàng đợi hành động (mọi user); Tenant, Application, Tích hợp (Admin)

## 3. Pattern dùng chung

- **Bảng dữ liệu**: `DataTable` — sort theo cột, phân trang client-side 10 dòng/trang, skeleton khi tải, empty state khi rỗng; hàng click-để-chọn mở panel chi tiết hoặc dialog sửa.
- **Form tạo/sửa**: Dialog; **filter** giữ inline trên bảng (`FilterBar`).
- **Badge trạng thái**: `StatusBadge` kiểu soft, map variant + label tiếng Việt tập trung tại `components/app/status.ts`.
- **Master–detail**: bảng trái + panel chi tiết phải (2 cột từ 1024px, mobile xếp dọc) — dùng ở Tài liệu, Duyệt tài liệu, Thư mục, Wiki, Phản hồi, Gợi ý quy trình, Hàng đợi hành động.
- **Xác nhận phá hủy**: `useConfirm()` — xóa thư mục, lưu trữ tenant/application, từ chối version/draft/correction/hành động, thực thi hành động, reset vector store, reset dữ liệu.
- **JSON**: `JsonViewer` pretty-print + copy (Hàng đợi hành động, Nguồn tri thức).

## 4. Admin flow

Màn hình: Tổng quan, Người dùng, Team, Thư mục, Nhật ký hệ thống, Cấu hình AI, Reset dữ liệu, Tenant, Application, Tích hợp.

- Người dùng/Team: bảng + dialog tạo (email, tên, vai trò, team chính, mật khẩu ban đầu).
- Thư mục: cây thụt lề + panel sửa tên/cha, checkbox quyền xem theo team, xóa mềm có xác nhận.
- Cấu hình AI: 4 thẻ trạng thái + tab Cơ bản (chọn provider, dán API key, preset tự áp) / Nâng cao (model, endpoint, embedding); nút Lưu / Lưu và test / Test cấu hình đã lưu.
- Reset dữ liệu: vùng nguy hiểm viền đỏ, nút chỉ mở khóa khi gõ đúng chuỗi xác nhận.

## 5. User flow

Màn hình: Tổng quan, Tài liệu, Hỏi AI, Gợi ý quy trình, Hàng đợi hành động.

- Tài liệu: filter thư mục/trạng thái/từ khóa; dialog tải lên (thư mục, tên, mô tả, file); panel chi tiết: tải bản gốc, tải phiên bản mới, bảng versions kèm trạng thái xử lý/lập chỉ mục và lý do từ chối.
- Hỏi AI: xem mục 7.

## 6. Reviewer flow

Màn hình thêm so với User: Duyệt tài liệu, Wiki, Phản hồi, Đánh giá, Giải thích truy xuất, Chỉ mục tri thức, Nguồn tri thức, Thư mục.

- Duyệt tài liệu: bảng chờ duyệt → panel version: Duyệt / Từ chối (kèm lý do, có xác nhận).
- Wiki: bảng draft → panel nội dung cuộn + tài liệu liên quan; Xuất bản (chọn phạm vi; phạm vi Toàn công ty bắt buộc tick xác nhận) / Từ chối (có xác nhận).
- Phản hồi: bảng phản hồi sai → panel: câu trả lời AI, nguồn (CitationCard), vấn đề chất lượng, correction (tạo draft → duyệt-và-lập-chỉ-mục / từ chối), tạo eval case, cập nhật trạng thái xử lý.
- Đánh giá: chạy tất cả case hoặc từng case; bảng lần chạy + kết quả pass/fail từng câu.
- Giải thích truy xuất: nhập câu hỏi + phạm vi → 6 thẻ thống kê pipeline, context cuối (kèm score/distance), bảng toàn bộ ứng viên.
- Chỉ mục tri thức: số chunk + rebuild (reset vector store phải xác nhận).

## 7. Màn hình Hỏi AI (demo chính)

- Prompt box: card bo tròn, textarea tự giãn 2→8 dòng; Enter gửi, Shift+Enter xuống dòng.
- Chip phạm vi (Popover): Tất cả nguồn được phép / Theo thư mục / Theo tài liệu đã lập chỉ mục; nút x quay về Tất cả.
- Đang xử lý: skeleton + "Đang tìm trong kho tri thức...".
- Câu trả lời: badge độ tin cậy (cao/trung bình/thấp), nút sao chép, callout khi AI cần thêm ngữ cảnh, khối "Thông tin còn thiếu" (vàng) và "Mâu thuẫn giữa các nguồn" (đỏ).
- Nguồn tham khảo: lưới 2 cột CitationCard — số thứ tự [n], icon theo loại nguồn, đường dẫn thư mục, đoạn trích 3 dòng. Chưa hỗ trợ bấm mở nguồn (backlog).
- Câu hỏi gợi ý tiếp theo: chip pill, bấm là điền và gửi lại.
- Feedback 👍/👎 — 👎 mở khung ghi chú gửi reviewer; kết quả báo toast.

## 8. Đăng nhập / Đổi mật khẩu

- Card căn giữa: mã tenant, email, mật khẩu; báo lỗi trong card; thông báo khi phiên hết hạn (`?expired=1`).
- `mustChangePassword` → chuyển sang màn đổi mật khẩu.
- Đăng xuất (menu user) đưa về màn đăng nhập.

## 9. Dashboard (Tổng quan)

Chỉ Admin/Reviewer thấy KPI; user thường thấy thông báo giới hạn quyền.

8 thẻ KPI: tài liệu đã duyệt, tài liệu chờ duyệt, wiki đã xuất bản, lượt hỏi AI, tỷ lệ feedback đúng, feedback sai chờ xử lý, eval case đang bật, tỷ lệ eval pass. Kèm bảng "Nguồn được trích dẫn nhiều". (Filter thời gian/team/folder: chưa có, thuộc backlog.)

## 10. Trạng thái chuẩn

- Loading: skeleton đúng hình dạng nội dung; spinner chỉ trong nút đang submit.
- Empty: icon + 1 câu tiếng Việt + action nếu có quyền.
- Error tải dữ liệu: khối đỏ nhạt + nút "Thử lại".
- Lỗi form: text đỏ dưới field hoặc trong dialog; lỗi thao tác: toast.
- AI không đủ thông tin: callout "cần thêm ngữ cảnh" + độ tin cậy thấp; processing/indexing thất bại: badge "Xử lý thất bại" trên version.
