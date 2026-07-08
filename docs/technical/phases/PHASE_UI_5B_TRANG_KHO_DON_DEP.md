# Phase UI 5b - Trang Khó Còn Lại + Dọn Dẹp

Ngày lập: 2026-07-08. Hoàn thành: 2026-07-09.

## Mục tiêu

Đợt cuối của [KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md](../../KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md): migrate 3 trang khó cuối cùng, xóa toàn bộ lớp legacy, lazy route, cập nhật tài liệu.

## Task đã thực hiện

1. **FeedbackReviewPage** (L) — tách `CorrectionCard` (hiển thị correction + nút Duyệt-và-lập-chỉ-mục / Từ chối, emit lên trang) và `EvalCaseForm` (v-model đáp án/từ khóa kỳ vọng); panel chi tiết: câu trả lời AI cuộn, nguồn qua CitationCard, vấn đề chất lượng + StatusBadge, form correction (phạm vi Company bắt buộc tick xác nhận, nút disable nếu chưa tick), **confirm trước khi từ chối correction**; toast thay message inline.
2. **AiSettingsPage** (L) — tách `useAiSettingsForm` composable (toàn bộ state/preset/payload — refactor thuần không đổi hành vi); UI Tabs **Cơ bản / Nâng cao**; 4 StatCard; kết quả test hiển thị badge OK/Thất bại. **Sai lệch so với kế hoạch**: không đưa vee-validate + zod vào — validation của form này thực tế chỉ là required + min/max đã có sẵn qua HTML attrs và logic `canSave`; thêm 2 dependency (~25KB gzip) không đáng. Nếu sau này form phức tạp hơn thì cân nhắc lại (ghi vào backlog).
3. **DataResetPage** — pattern DangerZone: card viền destructive, tiêu đề "Vùng nguy hiểm", nút chỉ mở khóa khi gõ đúng confirmation phrase; **sửa toàn bộ tiếng Việt không dấu thành có dấu**; kết quả reset dạng bảng dl.
4. **Dọn dẹp lớp legacy**:
   - Xóa `src/style.css` (~730 dòng), `layouts/LegacyLayout.vue`, `pages/PlaceholderPage.vue`, modal LLM cũ (nằm trong LegacyLayout).
   - `LayoutName` chỉ còn `'app' | 'auth'`; `main.ts` chỉ import `assets/main.css` + css vue-sonner.
   - Grep xác nhận repo không còn tham chiếu class nào của `style.css` (chỉ còn `clickable-rows` là prop của DataTable, không phải class).
5. **Lazy route**: toàn bộ 23 route chuyển `() => import(...)` — bundle chính giảm từ **543 kB (gzip 159 kB) xuống 142 kB (gzip 44 kB)**, mỗi trang tách chunk riêng.
6. **Cập nhật tài liệu**: viết lại `LUỒNG_GIAO_DIỆN.md` theo UI mới (layout, pattern dùng chung, flow theo role, màn Hỏi AI); cập nhật `HƯỚNG_DẪN_PHÁT_TRIỂN.md` (cấu trúc frontend thực tế, stack redesign, quy ước khi sửa UI, thêm gate lint).
7. `App.test.ts`: khóa cứng 23 route đều ở layout `app`/`auth`.

## Ghi nhận bundle size trước/sau toàn bộ redesign

| Thời điểm | JS chính (gzip) | CSS (gzip) |
| --- | --- | --- |
| Trước redesign (baseline đợt 0) | 428 kB (130 kB) | 71 kB (14 kB) |
| Sau đợt 2 (thêm tanstack + 6 trang) | 543 kB (159 kB) | — |
| Sau đợt 5b (lazy route, xóa legacy) | **142 kB (44 kB)** + chunk theo trang | 73 kB (13 kB) |

## Backlog sau redesign (giữ nguyên mục 8 kế hoạch, bổ sung)

Sidebar icon-rail + mobile Sheet; TreeView thật cho Thư mục; source viewer cho citation; chặn user đã đăng nhập vào `/login`; icon thông báo topbar; script screenshot Playwright; Prettier + plugin sort class Tailwind; cân nhắc lại vee-validate + zod cho AiSettings/correction nếu form phức tạp thêm; filter thời gian/team/folder cho Dashboard.

## Kết quả gate

- `npm run lint`: pass, 0 error 0 warning.
- `npm run build`: pass (vue-tsc strict), không còn cảnh báo chunk > 500 kB.
- `npm test`: 16/16 pass (5 file).
- Toàn bộ 23 route chạy trên AppLayout/AuthLayout; repo không còn `style.css`/`LegacyLayout`/`PlaceholderPage`.
