# Phase UI 2 - Admin CRUD

Ngày lập: 2026-07-08

## Mục tiêu

Đợt 2 của [KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md](../../KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md): xây `DataTable` generic + pattern Dialog form + ConfirmDialog áp dụng thật, migrate 6 trang admin CRUD và Việt hóa 3 trang tiếng Anh.

## Task đã thực hiện

1. **`DataTable.vue`** (generic `TData`, @tanstack/vue-table) — sort click header (icon mũi tên), global filter, phân trang client-side 10 dòng/trang (API list không có tham số phân trang — không đổi contract), skeleton 5 dòng khi loading, EmptyState khi rỗng, `clickableRows` + emit `rowClick` (pattern "click row đổ vào form" → mở Dialog edit), bảng cuộn ngang trong container riêng. Kèm `DataTablePagination.vue` (số dòng + Trang X/Y + prev/next).
2. **`FilterBar.vue`** — wrapper filter inline trên bảng (quyết định: create/edit dùng Dialog, filter giữ inline).
3. **`ConfirmDialog` dùng thật + sửa bug** — thay `AlertDialogAction/Cancel` bằng Button thường vì reka tự đóng dialog (fire `update:open` → settle(false)) TRƯỚC khi `@click` chạy → promise resolve sai; smoke test `ConfirmDialog.test.ts` bắt được bug này.
4. **`lib/format.ts`** — `formatDateTime` (vi-VN) thay 4 bản `formatDate` lặp.
5. **Migrate 6 trang** (đều `meta.layout: 'app'`, PageHeader + DataTable + toast + đủ loading/empty/error):
   - **UserManagement** — Dialog tạo user (email/tên/role/team/mật khẩu ban đầu), role label tiếng Việt, badge Hoạt động/Tạm khóa.
   - **TeamManagement** — Dialog tạo team.
   - **TenantManagement** (Việt hóa) — Dialog tạo/sửa (click row mở edit, mã tenant khóa khi sửa), **confirm trước khi lưu trữ**; cập nhật assertion `Phase8Pages.test.ts` cùng commit ("Tenant management" → "Quản trị tenant").
   - **ApplicationManagement** (Việt hóa) — FilterBar lọc tenant, Dialog tạo/sửa, confirm lưu trữ, map loại ứng dụng tiếng Việt.
   - **IntegrationManagement** (Việt hóa) — Dialog form 8 trường (secret nhập password, ghi chú mã hóa server-side), FilterBar lọc application, badge secret Đã cấu hình/Thiếu.
   - **AuditLog** — FilterBar hành động/loại đối tượng, cột thời điểm mới, metadata JSON `font-mono` truncate kèm title xem đầy đủ.

## Ghi chú kỹ thuật

- `SelectItem` của reka-ui không nhận `value=""` → filter "Tất cả" dùng sentinel `'all'`.
- Cột action (Lưu trữ) render qua `h(Button)` với `event.stopPropagation()` để không kích hoạt row-click.
- Bundle JS 543 kB (gzip 159 kB) — vượt ngưỡng cảnh báo 500 kB của Vite; đúng kế hoạch sẽ xử lý bằng lazy route theo nhóm ở đợt 5b.

## Kết quả gate

Hoàn thành: 2026-07-08. Lint pass (0 error 0 warning); build pass (vue-tsc strict); test **16/16 pass** (thêm DataTable.test.ts 4 case + ConfirmDialog.test.ts 2 case).
