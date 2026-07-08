# Kế Hoạch Thiết Kế Lại Giao Diện

Ngày lập: 2026-07-08

Tài liệu này là kế hoạch thiết kế + thi công lại toàn bộ lớp UI của frontend, dành cho người điều hành và AI coding agent. Mỗi đợt thi công bám theo quy ước phase của [KẾ_HOẠCH_THƯƠNG_MẠI_HÓA.md](KẾ_HOẠCH_THƯƠNG_MẠI_HÓA.md) mục 8.

## 1. Quyết Định Đã Chốt

| Hạng mục | Quyết định |
| --- | --- |
| Stack UI | **shadcn-vue (Reka UI) + Tailwind CSS v4**, icon lucide-vue-next |
| Phong cách | Tối giản hiện đại kiểu Linear/Notion: nhiều khoảng trắng, border thay shadow, 1 màu nhấn |
| Theme | Light + Dark (token hóa từ đầu, class `dark` trên `<html>`, 3 chế độ Sáng/Tối/Theo hệ thống) |
| Cách chuyển | Migrate từng nhóm màn hình; 2 shell chung sống (AppLayout mới + LegacyLayout cũ) qua nested routes |
| Giữ nguyên | Vue 3 + Vite + Pinia + vue-router, toàn bộ `src/api` (không đổi chữ ký), `stores/auth`, route path/name/meta, logic nghiệp vụ |

Nguyên tắc xuyên suốt:

- UI text tiếng Việt có dấu (kể cả aria-label, placeholder); text tiếng Anh sót lại phải Việt hóa khi migrate trang đó.
- On-prem: không phụ thuộc CDN — font self-host qua @fontsource, không gọi Google Fonts runtime.
- 3 gate sau MỖI đợt: `npm run lint`, `npm run build` (vue-tsc strict), `npm test`. Node 22 (Bash: `export PATH="/c/Users/Admin/AppData/Local/nvm/v22.14.0:$PATH"`).
- **Trạng thái dừng hợp lệ**: sau mỗi đợt, app phải ship được cho khách (2 shell tồn tại song song là trạng thái chấp nhận được). Riêng xóa `style.css` chỉ làm khi 100% trang đã migrate.
- Đội thi công: 1 dev + AI coding agent. Mỗi đợt ≤ 1 tuần công.

## 2. Hiện Trạng (kiểm kê 2026-07-08)

24 trang trong `src/pages` (23 route + 1 file mồ côi `PlaceholderPage.vue`), shell duy nhất trong `App.vue`, toàn bộ style trong `src/style.css` (~670 dòng class toàn cục). Các con số quyết định thiết kế component:

- **16/24 trang có bảng dữ liệu** — không trang nào có phân trang/sort/sticky header; 10 trang có hàng click-để-chọn.
- **9 trang master–detail** (bảng trái + panel chi tiết phải).
- **10 trang form tạo/sửa inline**; pattern "click row đổ vào form" ở Tenant/Application.
- **~20 trang** báo lỗi/thành công bằng `<p>` inline — không có toast; **không có confirm** trước bất kỳ hành động phá hủy nào (xóa folder, archive, reject); loading chỉ là đổi text nút; skeleton/empty state gần như không có.
- Badge trạng thái: 1 kiểu xanh cho mọi trạng thái ở 6 trang, các trang khác in enum thô.
- Code lặp: `flattenFolders` copy y hệt ở 4 file; scope selector trùng 100% giữa 2 trang; helper xử lý ApiError lặp ≥ 9 file.
- Trang tiếng Anh cần Việt hóa: Tenant/Application/Integration/KnowledgeSource/Recommendation/ActionQueue; `DataResetPage` tiếng Việt không dấu.
- Font khai báo `Inter` nhưng không self-host (máy không cài Inter rơi về system font).

Bảng kiểm kê từng trang: xem Phụ lục A.

## 3. Design System

### 3.1 Design tokens

Khai báo trong `src/assets/main.css` theo convention shadcn (`:root` / `.dark`), dùng qua utility (`bg-background`, `text-muted-foreground`...). **Cấm hardcode hex trong component.**

| Token | Light (HSL) | Dark (HSL) | Ghi chú |
| --- | --- | --- | --- |
| `--background` | `0 0% 100%` | `224 15% 8%` | Dark ngả xanh kiểu Linear |
| `--foreground` | `224 20% 12%` | `220 14% 92%` | |
| `--card` / `--card-foreground` | `0 0% 100%` / `224 20% 12%` | `224 14% 11%` / `220 14% 92%` | Dark: card sáng hơn nền 1 nấc |
| `--popover` / `--popover-foreground` | `0 0% 100%` / `224 20% 12%` | `224 14% 12%` / `220 14% 92%` | |
| `--muted` / `--muted-foreground` | `220 14% 96%` / `220 9% 42%` | `223 12% 16%` / `220 9% 62%` | thead, code block, skeleton / text phụ |
| `--accent` / `--accent-foreground` | `220 14% 95%` / `224 20% 12%` | `223 12% 18%` / `220 14% 92%` | Hover row/menu |
| `--primary` / `--primary-foreground` | `243 75% 59%` / trắng | `239 84% 67%` / trắng | Màu nhấn — xem 3.2 |
| `--destructive` / `--destructive-foreground` | `0 72% 51%` / trắng | `0 84% 60%` / trắng | |
| `--border` / `--input` | `220 13% 91%` / `220 13% 89%` | `223 12% 20%` / `223 12% 22%` | |
| `--ring` | = primary | = primary | Focus ring |
| `--success` | `142 72% 36%` | `142 69% 45%` | Token bổ sung |
| `--warning` | `32 95% 44%` | `38 92% 50%` | Token bổ sung |
| `--info` | `221 83% 53%` | `217 91% 60%` | Token bổ sung |
| `--radius` | `0.5rem` | | Card/dialog 8px, button/input 6px, badge full |

### 3.2 Màu nhấn

Khuyến nghị chính: **Indigo** (`#4f46e5` light / `#6366f1` dark) — gợi "AI/trí tuệ", đúng chất Linear, tách khỏi xanh admin phổ thông. Thay thế: (a) Teal `#0d9488` — tri thức/tin cậy; (b) Blue `#2563eb` — kế thừa `#1f6feb` hiện tại. Đổi accent chỉ cần sửa 2 dòng `--primary`/`--ring`. Khi chốt token, chạy WCAG contrast checker 1 lần: body text và text-trên-badge phải đạt AA 4.5:1 ở cả 2 theme.

### 3.3 Badge trạng thái nghiệp vụ

- 1 helper tập trung `statusVariant(status): 'success' | 'warning' | 'destructive' | 'info' | 'muted'` + map label tiếng Việt, đặt tại `components/app/status.ts`. **Sinh từ đúng union type trong `src/api/*.ts`** — dùng `Record<EnumType, ...>` để vue-tsc bắt thiếu/thừa case (lưu ý: confidence API là chữ thường `'high' | 'medium' | 'low'`).
- Quy tắc map: `success` = Approved/Indexed/Published/Correct/Resolved/pass; `warning` = PendingReview/Draft/InReview/Processing-có-cảnh-báo; `destructive` = Rejected/Incorrect/Failed/fail; `info` = New/Processing; `muted` = Archived/Disabled/None.
- Kiểu **soft**: nền `hsl(var(--x) / 0.12)` (dark `/0.18`), chữ + border cùng token. Không nền đặc.

### 3.4 Typography

- **Font chính: Inter Variable** (`@fontsource-variable/inter`, subset `vietnamese` + `latin`) — dấu tiếng Việt đặt đúng, đọc tốt ở 13-14px trong bảng dày, 1 file variable woff2 hợp on-prem. (Thay thế: Be Vietnam Pro — đẹp hơn ở heading lớn nhưng phải load 4-5 weight.)
- **Font mono: stack hệ thống** `ui-monospace, SFMono-Regular, Consolas, monospace` — không thêm package (quyết định sau phản biện: JetBrains Mono không đáng thêm ~100KB).
- Scale: page title `text-2xl font-semibold tracking-tight` (1 cái/trang); tiêu đề card `text-lg font-semibold`; label/th/menu `text-sm font-medium`; body mặc định `text-sm` (14/22); riêng answer AI + nội dung wiki `text-base leading-7`; caption `text-xs text-muted-foreground`; KPI `text-3xl font-semibold tabular-nums`.
- Không dùng weight 700+ ngoài KPI; nhấn bằng weight 500/600 + màu; body tiếng Việt line-height ≥ 1.5.

### 3.5 App shell mới (AppLayout)

- **Sidebar** trái cố định `w-64`, nền `background`, phân tách `border-r`. V1 KHÔNG làm thu gọn icon-rail và mobile Sheet (đưa vào backlog — xem mục 8). Item active: nền `accent` + thanh chỉ báo 2px `primary` bên trái.
- **Menu config-driven, khai báo role theo TỪNG item đúng meta router** (không nhóm cứng theo role — lưu ý `/admin/folders` là `requiresReviewer` chứ không phải admin):
  - *Làm việc*: Tổng quan, Tài liệu, Hỏi AI, Wiki
  - *Chất lượng* (Reviewer+): Duyệt tài liệu, Phản hồi, Đánh giá, Giải thích truy xuất, Chỉ mục tri thức, Nguồn tri thức, **Thư mục**
  - *Quản trị* (Admin): Người dùng, Team, Cấu hình AI, Nhật ký hệ thống, Reset dữ liệu
  - *Nền tảng*: Gợi ý quy trình, Hàng đợi hành động (mọi user); Tenant, Application, Tích hợp (Admin)
  - Mỗi item có icon lucide 16px; label nhóm `text-xs uppercase text-muted-foreground`.
- **Topbar** `h-14 border-b`: toggle sidebar, tên trang hiện tại (thay h1 tĩnh), bên phải: badge tenant, `ThemeToggle` (3 chế độ), user menu (avatar chữ cái → tên/email/role + Đăng xuất). **Đăng xuất phải gọi `logout()` rồi `router.push({ name: 'login' })`** (hiện logout không điều hướng — sẽ thành bug khi login tách layout riêng).
- **Cảnh báo LLM chưa cấu hình**: trong AppLayout là **banner mỏng màu warning dưới topbar** (dismiss được, Admin có link "Cấu hình ngay"); LegacyLayout giữ nguyên modal cũ đến đợt cuối.
- **Content**: `p-6` (mobile `p-4`), `max-w-6xl` cho trang quản trị; AI Q&A và Login dùng khối hẹp căn giữa. PageHeader pattern: tiêu đề + mô tả bên trái, action chính bên phải, margin-bottom 24px. Master–detail: 2 cột `minmax(0,1fr) / minmax(320px, 0.9fr)`, mobile xếp dọc.
- **Elevation kiểu Linear**: card phẳng chỉ `border`; dropdown `shadow-md`; dialog `shadow-lg`; không shadow màu, không shadow hover.
- AppLayout bọc toàn trang bằng `div.min-h-screen.bg-background.text-foreground` — bắt buộc, vì `style.css` cũ (unlayered) set màu trên `:root` sẽ đè theme mới nếu chỉ dựa vào `@layer base`.

### 3.6 Trạng thái chuẩn (thống nhất toàn app)

- **Loading**: skeleton đúng hình dạng nội dung (bảng 5 dòng, KPI theo số ô, detail 3 khối); spinner chỉ trong button đang submit. Không spinner toàn trang.
- **Empty state**: component `EmptyState` — icon lucide 40px `muted-foreground/50`, 1 câu tiếng Việt, kèm action nếu user có quyền.
- **Error state**: khối `border-destructive/30 bg-destructive/10 text-destructive rounded-md p-3 text-sm` + nút "Thử lại" với lỗi tải dữ liệu.
- **Form**: viền lỗi `border-destructive` + message `text-xs text-destructive` dưới field, `aria-invalid`; lỗi API hiện toast trừ khi map được về field. **Create/edit dùng Dialog form; filter giữ inline `FilterBar`** (chốt cho pattern "click row đổ vào form" cũ).
- **Toast**: vue-sonner, bottom-right, tự đóng 5s — thay toàn bộ pattern `successMessage/errorMessage` ref.
- **Confirm**: `useConfirm()` dạng promise + AlertDialog — bắt buộc cho mọi hành động phá hủy (xóa folder, archive, reject, reset index...).

### 3.7 Dark mode

- Composable `useTheme()` 3 chế độ light/dark/system dựa trên `useColorMode` của VueUse (key localStorage `theme`, mặc định system, lắng nghe `prefers-color-scheme`). Inline script trong `index.html` set class trước khi Vue mount (chống FOUC). `color-scheme: light dark` trên `:root`.
- **Trang legacy luôn light trong giai đoạn chuyển tiếp; ThemeToggle chỉ hiện ở AppLayout** — đây là quyết định chính thức, không tính là bug khi nghiệm thu.
- Bẫy phải kiểm: contrast badge soft ở dark (alpha `/0.18`), phân tầng bằng border + surface (không tăng shadow), `pre`/code đọc nền từ `muted`, overlay dialog `bg-black/50` cả 2 theme.

### 3.8 Accessibility tối thiểu

Focus ring `focus-visible:ring-2 ring-ring` mọi element tương tác (không `outline: none` trần); hàng bảng click được phải dùng được bằng bàn phím; click target ≥ 36px; label gắn `for/id`; dialog trap focus + Esc (Reka UI có sẵn); icon-button có `aria-label` tiếng Việt.

## 4. Đặc Tả Riêng Trang AI Q&A (màn hình demo bán hàng — mức L)

- Layout cột đơn căn giữa `max-w-3xl`, không bọc panel quản trị.
- **Prompt box**: card `rounded-xl border` focus-within `ring-2 ring-ring/40`; textarea không viền riêng, auto-grow 2→8 dòng; hàng dưới: scope chips bên trái (Popover chọn Tất cả/Folder/Tài liệu, chip có icon + tên + nút x), nút gửi primary bên phải; Enter gửi, Shift+Enter xuống dòng.
- **Loading**: skeleton 4-5 dòng + "Đang tìm trong kho tri thức..." — cảm giác streaming dù API trả một lần.
- **Answer card**: badge confidence map token (`'high'`→success "Độ tin cậy cao", `'medium'`→warning, `'low'`→destructive) + nút copy; thân `text-base leading-7 whitespace-pre-wrap` (bỏ thẻ `pre` font mono hiện tại); `needsClarification` → callout info.
- **Citations**: grid 2 cột (mobile 1), mỗi card: số `[1]` ô vuông primary-soft, icon theo sourceType, title 1 dòng, folder path + section `text-xs font-mono`, excerpt `line-clamp-3`. **V1 không click-through** (chưa có API xem nội dung nguồn — "source viewer" vào backlog).
- **Missing info / Conflicts**: 2 callout viền trái 2px (warning / destructive).
- **Follow-up chips**: pill `rounded-full`, click điền vào prompt box và submit.
- **Feedback**: icon 👍/👎; 👎 mở khung note nhỏ; kết quả báo bằng toast.
- Transition opacity/translate-y 150ms khi answer xuất hiện; không gradient.

## 5. Kiến Trúc Kỹ Thuật

### 5.1 Dependencies thêm mới

| Package | Loại | Vai trò |
| --- | --- | --- |
| `tailwindcss` v4 + `@tailwindcss/vite` | dev | Utility CSS. **V4 khác v3**: KHÔNG có `tailwind.config.js`/PostCSS; cấu hình bằng CSS (`@theme`, `@custom-variant`); nạp bằng `@import "tailwindcss"` |
| `shadcn-vue` CLI (qua npx) + `reka-ui` | dev/runtime | Generate component vào repo; primitives headless |
| `class-variance-authority`, `clsx`, `tailwind-merge`, `tw-animate-css` | runtime/dev | Bộ đi kèm shadcn (`cn()`, variant, animation cho v4) |
| `lucide-vue-next` | runtime | Icon; CHỈ import đích danh, cấm wildcard |
| `@vueuse/core` | runtime | `useColorMode`, debounce, onClickOutside |
| `@tanstack/vue-table` | runtime | Bảng headless cho `DataTable` wrapper (16 trang bảng) — ~14KB gzip, trả 1 lần |
| `vee-validate` + `@vee-validate/zod` + `zod` | runtime | **Chỉ cho form phức tạp** (AiSettings, Integration, upload, correction). Form CRUD nhỏ 2-3 field giữ v-model + validate thủ công — không bắt viết zod schema cho mọi form |
| `vue-sonner` | runtime | Toast |
| `@fontsource-variable/inter` | runtime | Font self-host subset vietnamese |
| `@testing-library/vue` | dev | Smoke test component chung |

### 5.2 Cấu trúc thư mục

```text
src/
├── components/
│   ├── ui/        # shadcn generate — không sửa tay tùy tiện; buộc sửa thì ghi "// [đã sửa tay]" đầu file
│   └── app/       # component nghiệp vụ: AppSidebar, AppTopbar, PageHeader, DataTable, FilterBar,
│                  # ConfirmDialog, StatusBadge (+ status.ts), EmptyState, ErrorState, CitationCard,
│                  # ThemeToggle, FolderSelect, ScopeSelector, JsonViewer, FileUpload, StatCard...
├── layouts/       # AppLayout.vue (mới) / LegacyLayout.vue (tách từ App.vue cũ) / AuthLayout.vue
├── composables/   # useTheme, useConfirm, useToast, usePagination, useApiAction, useFolderTree
├── lib/utils.ts   # cn()
└── assets/main.css # tailwind + tokens + font
```

Test đặt cạnh file nguồn (`DataTable.test.ts`). `App.vue` rút còn `<RouterView/>` + `<Toaster/>`.

### 5.3 Cơ chế 2 shell chung sống

- **Nested routes 3 record cha**: AppLayout / LegacyLayout / AuthLayout; children giữ nguyên path tuyệt đối + name; guard `beforeEach` giữ nguyên. Mỗi đợt migrate = cắt route record từ nhóm Legacy dán sang nhóm App.
- **Test điều hướng bắt buộc** (phase 0): với mỗi route name, assert `router.resolve` trả về đúng cặp (layout, page) — 3 record cha cùng `path: '/'` có rủi ro matcher; nếu trục trặc, phương án dự phòng là 1 record cha duy nhất + switch layout theo `meta.layout`.
- **CSS chung sống 2 chiều**:
  - Chiều 1 — preflight Tailwind (trong `@layer base`) vs `style.css` (unlayered): CSS unlayered thắng khi trùng property nên trang cũ phần lớn tự bảo toàn; rủi ro là style cũ *dựa vào default trình duyệt* bị preflight reset (margin heading, list-style, border input). Sau phase 0 rà toàn bộ trang cũ 1 lượt, vá bằng ~10-20 dòng "compat" thêm vào CUỐI `style.css` (lần sửa style.css duy nhất được phép).
  - Chiều 2 — `style.css` đè trang mới: `:root { color; background }` unlayered đè theme ở mọi theme → AppLayout bọc `div.bg-background.text-foreground` (mục 3.5).
  - Thứ tự import trong `main.ts`: `assets/main.css` TRƯỚC, `style.css` SAU.

### 5.4 Quy ước thi công cho AI agent (mỗi trang migrate)

1. Bắt buộc dùng component chung; thiếu thì tạo trong `components/app/` trước, không viết inline một-lần.
2. CẤM trong trang đã migrate: class từ `style.css`, thẻ `<style>` mới, hardcode hex, import icon wildcard, tạo `tailwind.config.js`/`postcss.config.js`/`@tailwind base` (đó là cú pháp v3 — thấy là sai).
3. Giữ nguyên chữ ký `src/api`, store, route path/name/meta; chỉ đổi template + class; tách composable là refactor thuần không đổi hành vi.
4. Checklist nghiệm thu từng trang: (a) đúng ở cả light + dark, không màu hardcode lộ khi đổi theme; (b) đủ 3 trạng thái loading/empty/error; (c) responsive 1280/1024 (bảng cuộn ngang trong container riêng)/768px; (d) hành vi CRUD chính chạy thật với backend.
5. Commit theo nếp repo, mỗi đợt 1-2 commit, pass 3 gate trước khi commit.

### 5.5 Kiểm soát chất lượng

- **Phase 0 bắt buộc**: thêm vitest `setupFiles` stub `ResizeObserver`/`matchMedia`/`scrollIntoView` (reka-ui cần, jsdom không có — thiếu là test vỡ runtime); viết lại `App.test.ts` (hiện assert text của shell cũ — chắc chắn vỡ); thêm block ESLint tắt `vue/multi-word-component-names`, `vue/require-default-prop`, `vue/require-prop-types` CHỈ cho `src/components/ui/**`.
- `Phase8Pages.test.ts` khóa cứng text tiếng Anh — cập nhật assertion **trong cùng commit** Việt hóa trang tương ứng (đợt 2: Tenant; đợt 5a: 3 trang còn lại).
- Smoke test lớp `app/` + composables (không test `ui/` generated): `DataTable.test.ts`, `StatusBadge.test.ts`, `ConfirmDialog.test.ts`, `useTheme.test.ts`.
- Pagination của DataTable là **client-side** trên dữ liệu đã tải (API list hiện không có tham số phân trang — không đổi contract).
- Screenshot nghiệm thu: chạy dev, chụp mỗi trang 2 ảnh light/dark (set `localStorage.theme` rồi reload); có thể script hóa bằng Playwright sau, không đưa vào CI gate.

## 6. Lộ Trình Thi Công (7 đợt × ≤ 1 tuần)

Mỗi đợt tạo 1 file phase chi tiết `docs/technical/phases/PHASE_UI_<n>_<tên>.md` theo quy ước, với acceptance chung: (i) danh sách trang hoàn thành, (ii) checklist 5.4 từng trang, (iii) 3 gate pass, (iv) 1 luồng nghiệp vụ demo chạy thật với backend.

### Đợt 0 — Hạ tầng (không migrate trang nào ngoài auth)

- Alias `@/` (vite + tsconfig); cài Tailwind v4 + init shadcn-vue (style new-york, base neutral); tokens light/dark + accent; font Inter Variable; inline script FOUC; add đợt component `ui/` đầu tiên (button, input, label, select, dialog, dropdown-menu, table, badge, card, skeleton, separator, sheet, tooltip, sonner, form, checkbox, textarea, tabs, alert).
- Tách `LegacyLayout.vue` từ `App.vue` (giữ nguyên markup + modal LLM); tạo `AppLayout` (sidebar config-driven + topbar + banner LLM) + `AuthLayout`; router chuyển nested 3 record cha; test `router.resolve` từng route name.
- Vitest setupFiles; viết lại `App.test.ts`; ESLint override cho `ui/`; `useTheme` + `useToast` + `useConfirm`.
- Rà preflight regression toàn bộ trang cũ, vá compat vào cuối `style.css`.
- Migrate **LoginPage + ChangePasswordPage** vào AuthLayout (định hình token form/button); sửa logout điều hướng về login.
- Acceptance: trang chưa migrate chạy nguyên trong LegacyLayout không đổi hành vi; login/logout/đổi mật khẩu chạy thật; contrast check token pass; 3 gate pass.

### Đợt 1 — Component lõi + Dashboard

- `PageHeader`, `StatCard`, `EmptyState`, `ErrorState`, `StatusBadge` + `status.ts` (map đủ union type từ `src/api`), skeleton pattern.
- Migrate **DashboardPage** (8 StatCard + bảng nguồn trích dẫn dùng `ui/table` tĩnh — CHƯA cần DataTable generic).
- Acceptance: Dashboard đẹp ở 2 theme, có skeleton + empty state; smoke test StatusBadge.

### Đợt 2 — Admin CRUD đơn giản (6 trang, "trả nợ" component nhanh nhất)

- Xây `DataTable` generic (@tanstack/vue-table: sort, filter, pagination client-side, row-selection) + `DataTablePagination` + `FilterBar` + Dialog form pattern + `ConfirmDialog` áp dụng thật.
- Migrate: **UserManagement, TeamManagement, TenantManagement, ApplicationManagement, IntegrationManagement, AuditLog**. Việt hóa Tenant/Application/Integration; confirm cho Archive; cập nhật `Phase8Pages.test.ts` (phần Tenant) cùng commit.
- Acceptance: 6 trang + smoke test DataTable/ConfirmDialog; tạo-sửa-archive chạy thật.

### Đợt 3 — Documents + Folders + Review tài liệu

- Component mới: `FolderSelect` + `useFolderTree` (dẹp 4 bản `flattenFolders`), `FileUpload`, layout master–detail.
- Migrate: **DocumentListPage** (L — 2 form + 2 bảng + upload + versions), **DocumentReviewPage** (thêm confirm trước reject), **FolderManagementPage** (L — v1 dùng danh sách thụt lề style mới, TreeView thật vào backlog; permission checkbox list + confirm xóa).
- Acceptance: luồng upload → duyệt → index chạy thật end-to-end trên UI mới.

### Đợt 4 — Mặt tiền AI (làm khi token/component đã chín)

- Component đặc thù: `CitationCard` (dùng ở 5 trang), `ScopeSelector`, `AnswerPanel`.
- Migrate: **AiQuestionPage** (mức **L** — theo đặc tả mục 4, cần nhiều vòng polish), **RetrievalExplainPage** (tái dùng ScopeSelector/CitationCard + hiển thị score/distance dạng `text-xs font-mono` trên CitationCard), **WikiDraftPage** (ContentViewer cho nội dung dài + confirm-checkbox publish Company).
- Acceptance: demo hỏi đáp có citation "trông như sản phẩm AI 2026" ở cả 2 theme; luồng hỏi → feedback chạy thật.

### Đợt 5a — Review nâng cao + Workflow

- Mini-spec trước khi làm: `JsonViewer` (khối `<pre>` scroll + copy, dùng cho 4 khối JSON của ActionQueue + KnowledgeSource + AuditLog cell), action bar theo state machine (ActionQueue 8 trạng thái).
- Migrate: **RecommendationListPage, ActionApprovalQueuePage** (Việt hóa + cập nhật `Phase8Pages.test.ts` cùng commit), **KnowledgeSourcePage** (Việt hóa), **EvaluationPage**, **KnowledgeIndexPage** (confirm khi reset vector store).
- Acceptance: luồng recommendation → action request → approve/execute chạy thật.

### Đợt 5b — Trang khó còn lại + dọn dẹp

- Migrate: **FeedbackReviewPage** (L — tách `CorrectionCard`, `EvalCaseForm`; vee-validate cho form correction), **AiSettingsPage** (L — tách `useAiSettingsForm` composable trước, UI dùng Tabs Cơ bản/Nâng cao; vee-validate + zod), **DataResetPage** (pattern DangerZone: card viền destructive + confirmation phrase; sửa tiếng Việt có dấu).
- Dọn dẹp: xóa `style.css` + `LegacyLayout` + `PlaceholderPage` + modal LLM cũ; chuyển route sang lazy import theo nhóm; rà 3 gate + checklist toàn cục lần cuối; cập nhật `HƯỚNG_DẪN_PHÁT_TRIỂN.md` và `LUỒNG_GIAO_DIỆN.md` theo UI mới.
- Acceptance: repo không còn tham chiếu class `style.css`; bundle size ghi nhận trước/sau; toàn bộ 23 route chạy trên AppLayout/AuthLayout.

## 7. Rủi Ro Và Cách Né

| Rủi ro | Cách né |
| --- | --- |
| AI agent sinh cú pháp Tailwind v3 (`tailwind.config.js`, `@tailwind base`, `darkMode: 'class'`) | Quy ước 5.4 mục 2 — cấu hình chỉ nằm trong `main.css`; thấy file config v3 là lỗi |
| shadcn-vue là code copy, không có npm update | Chốt version CLI trong `components.json`; sửa tay file `ui/` phải đánh dấu; add đích danh, không re-init |
| `style.css` unlayered đè theme mới / preflight phá trang cũ | Cơ chế 5.3 (wrapper div + compat CSS + thứ tự import) |
| jsdom thiếu API reka-ui cần | setupFiles stub ở đợt 0 |
| vue-tsc strict + `erasableSyntaxOnly` vs code generated | `skipLibCheck` đã bật; file generated fail thì sửa tối thiểu + đánh dấu, không nới tsconfig; gặp enum thì đổi sang union type |
| Bundle size tăng (zod + vee-validate + tanstack ≈ 40KB gzip) | Giới hạn zod cho form phức tạp; cấm lucide wildcard; ghi nhận size mỗi đợt; lazy route ở 5b |
| Matcher vue-router với 3 record cha cùng `path: '/'` | Test `router.resolve` ở đợt 0; dự phòng `meta.layout` |
| Phải dừng giữa chừng vì ưu tiên khác | Nguyên tắc "trạng thái dừng hợp lệ" mục 1 — mỗi đợt kết thúc đều ship được |

## 8. Backlog Sau Redesign

Sidebar thu gọn icon-rail + mobile Sheet (drawer); TreeView thật cho FolderManagement (Reka UI Tree); source viewer cho citation click-through (cần API đọc nội dung nguồn); chặn user đã đăng nhập vào `/login`; icon thông báo trên topbar (chờ tính năng notification); script screenshot Playwright tự động; Prettier + plugin Tailwind class sorting.

## Phụ Lục A — Kiểm Kê Trang (2026-07-08)

| Trang | Route/guard | Khối UI chính | Độ khó | Đợt |
| --- | --- | --- | --- | --- |
| auth/LoginPage (69d) | `/login` public | 1 form + báo `?expired=1` | S | 0 |
| auth/ChangePasswordPage (55d) | `/change-password` | 1 form | S | 0 |
| DashboardPage (108d) | `/` | 8 stat-card + 1 bảng | S | 1 |
| admin/UserManagementPage (106d) | `/admin/users` admin | form + bảng | S | 2 |
| admin/TeamManagementPage (71d) | `/admin/teams` admin | form + bảng | S | 2 |
| admin/TenantManagementPage (173d) | `/admin/tenants` admin | form edit-in-place + bảng + archive; EN | M | 2 |
| admin/ApplicationManagementPage (220d) | `/admin/applications` admin | filter + form + bảng + archive; EN | M | 2 |
| admin/IntegrationManagementPage (167d) | `/admin/integrations` admin | form 8 trường (secret) + bảng; EN | M | 2 |
| admin/AuditLogPage (64d) | `/admin/audit-logs` admin | filter + bảng (JSON trong cell) | S | 2 |
| documents/DocumentListPage (223d) | `/documents` | upload + filter + 2 bảng + master–detail + versions | L | 3 |
| review/DocumentReviewPage (98d) | `/review` reviewer | bảng pending + approve/reject per version | M | 3 |
| admin/FolderManagementPage (223d) | `/admin/folders` **reviewer** | cây folder + permission matrix + xóa mềm | L | 3 |
| ai/AiQuestionPage (186d) | `/ai` | prompt + answer + citations + feedback | **L** (spec mục 4) | 4 |
| review/RetrievalExplainPage (203d) | `/retrieval-explain` reviewer | scope + 6 stat + citations kèm score + bảng candidates | M/L | 4 |
| wiki/WikiDraftPage (148d) | `/wiki` reviewer | bảng drafts + detail + publish/reject | M | 4 |
| workflow/RecommendationListPage (276d) | `/workflow/recommendations` | filter + bảng + detail + feedback + tạo action; EN | L | 5a |
| workflow/ActionApprovalQueuePage (256d) | `/workflow/actions` | filter 5 trường + bảng + 4 khối JSON + 3 action theo status; EN | L | 5a |
| review/KnowledgeSourcePage (174d) | `/knowledge-sources` reviewer | 2 bảng + detail JSON; EN | M | 5a |
| review/EvaluationPage (162d) | `/evaluation` reviewer | form chạy eval + 3 bảng | M | 5a |
| review/KnowledgeIndexPage (129d) | `/knowledge-index` reviewer | 3 stat + form rebuild (cần confirm) | S/M | 5a |
| review/FeedbackReviewPage (294d) | `/feedback` reviewer | bảng + detail 4 form lồng nhau + corrections | L | 5b |
| admin/AiSettingsPage (443d) | `/admin/ai-settings` admin | 4 stat + form provider preset + advanced ~12 trường | L | 5b |
| admin/DataResetPage (113d) | `/admin/data-reset` admin | danger zone confirmation phrase; VN không dấu | S/M | 5b |
| PlaceholderPage (14d) | không route nào dùng | — | xóa | 5b |

## Tài Liệu Liên Quan

- [Kế hoạch thương mại hóa](KẾ_HOẠCH_THƯƠNG_MẠI_HÓA.md) — quy ước phase cho AI agent (mục 8)
- [Luồng giao diện hiện tại](../LUỒNG_GIAO_DIỆN.md) — cập nhật sau đợt 5b
- [Quy tắc code](../QUY_TẮC_CODE.md)
- [Phase 1 pre-sales hardening](technical/phases/PHASE_1_PRE_SALES_HARDENING.md) — đã hoàn thành, nền ESLint/strict mà kế hoạch này dựa vào
