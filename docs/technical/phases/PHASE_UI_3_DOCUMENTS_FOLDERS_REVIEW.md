# Phase UI 3 - Documents + Folders + Review

Ngày lập: 2026-07-08

## Mục tiêu

Đợt 3 của [KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md](../../KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md): dẹp code lặp `flattenFolders` (4 bản copy), component upload file, layout master–detail, migrate 3 trang nghiệp vụ tài liệu.

## Task đã thực hiện

1. **`useFolderTree` composable + `flattenFolderTree`** — tải cây folder + duỗi thành danh sách thụt lề (trước đây copy y hệt ở 4 file).
2. **`FolderSelect.vue`** — Select folder thụt lề theo cấp; v-model `''` = không chọn (map sentinel nội bộ vì reka không nhận value rỗng); prop `noneLabel` ("Tất cả thư mục"/"Thư mục gốc"), `disabledId` (chặn folder tự làm cha chính nó).
3. **`FileUpload.vue`** — vùng chọn file viền dashed, hiện tên + kích thước file đã chọn, nút bỏ chọn, v-model `File | null`.
4. **Migrate 3 trang** (meta.layout `'app'`, master–detail `lg:grid-cols-[minmax(0,1fr)_minmax(320px,0.9fr)]`, mobile xếp dọc):
   - **DocumentListPage** (L) — Dialog upload (FolderSelect + FileUpload); FilterBar thư mục/trạng thái/từ khóa; DataTable click-để-chọn; panel chi tiết: tải bản hiện tại, tạo wiki draft (đúng điều kiện Approved + Indexed cũ), upload phiên bản mới, bảng versions với StatusBadge + metadata trích xuất (cảnh báo màu warning).
   - **DocumentReviewPage** — bảng chờ duyệt + panel version card; **confirm trước khi từ chối**; lý do reject nhập riêng từng version (trước dùng chung 1 ô); toast thay message inline.
   - **FolderManagementPage** (L) — danh sách thụt lề style mới (padding theo depth, TreeView thật để backlog); panel chi tiết: form sửa cha/tên, danh sách checkbox quyền theo team (ui/checkbox), khối xóa viền destructive + **confirm trước xóa mềm**; Dialog tạo thư mục.

## Kết quả gate

Hoàn thành: 2026-07-08. Lint pass; build pass (vue-tsc strict); test 16/16 pass. Luồng upload → duyệt → index cần demo chạy thật với backend khi nghiệm thu (UI giữ nguyên chữ ký API cũ nên hành vi không đổi).
