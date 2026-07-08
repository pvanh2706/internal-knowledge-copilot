# Phase UI 0 - Hạ Tầng Redesign

Ngày lập: 2026-07-08

## Mục tiêu

Đặt nền móng design system mới theo [KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md](../../KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md) (đợt 0): Tailwind v4 + shadcn-vue, tokens light/dark, cơ chế 2 shell chung sống, composables nền tảng, và migrate 2 trang auth đầu tiên. Không migrate trang nào khác ngoài auth.

## File/module liên quan

- `src/frontend/src/assets/main.css` — tokens + Tailwind v4 (cấu hình bằng CSS, KHÔNG có tailwind.config.js)
- `src/frontend/src/components/ui/**` — shadcn-vue generate (20 bộ component)
- `src/frontend/src/components/app/` — AppSidebar, AppTopbar, ThemeToggle, ConfirmDialog, LlmWarningBanner, navigation.ts
- `src/frontend/src/layouts/` — AppLayout / AuthLayout / LegacyLayout
- `src/frontend/src/composables/` — useTheme, useToast, useConfirm
- `src/frontend/src/router/index.ts`, `src/frontend/src/App.vue`, `src/frontend/src/main.ts`, `index.html`
- `src/frontend/src/pages/auth/` — LoginPage, ChangePasswordPage (đã migrate)

## Task đã thực hiện

1. **Alias `@/`** — vite `resolve.alias` + tsconfig `paths` (không dùng `baseUrl` — TS 6 deprecated).
2. **Tailwind v4 + shadcn-vue** — cài `tailwindcss` + `@tailwindcss/vite` + `tw-animate-css`; init `components.json` (style new-york, base neutral); generate 20 bộ component ui/ (button, input, label, select, dialog, dropdown-menu, table, badge, card, skeleton, separator, sheet, tooltip, sonner, checkbox, textarea, tabs, alert, alert-dialog, popover). Component `form` hoãn đến đợt 5b (kèm vee-validate).
3. **Tokens light/dark** — khai báo HSL trong `main.css` theo bảng mục 3.1 của kế hoạch; accent Indigo. **Điều chỉnh sau khi chạy WCAG contrast checker**: token trạng thái light đậm hơn 1 nấc (`success 142 76% 27%`, `warning 28 95% 34%`, `destructive 0 72% 42%`, `info 221 83% 45%`) và destructive/info dark sáng hơn 1 nấc (`0 84% 65%` / `217 91% 65%`) để chữ trên badge soft đạt AA 4.5:1 ở cả 2 theme. Body text light 17.07:1, dark 15.38:1 — pass.
4. **Font Inter Variable** — `@fontsource-variable/inter` self-host; import `index.css` (unicode-range nên trình duyệt chỉ tải subset vietnamese/latin thực dùng). Mono dùng stack hệ thống.
5. **Dark mode** — `useTheme()` (VueUse `useColorMode`, key `theme`, 3 chế độ); inline script chống FOUC trong `index.html`; `color-scheme: light dark`.
6. **2 shell chung sống** — LegacyLayout tách nguyên trạng từ App.vue (kèm modal LLM cũ); AppLayout mới (sidebar w-64 config-driven theo `components/app/navigation.ts`, khai báo role từng item khớp meta router; topbar h-14 với tenant badge + ThemeToggle + user menu; banner LLM warning mỏng dismiss được); AuthLayout căn giữa.
   - **Quyết định kiến trúc**: dùng phương án dự phòng `meta.layout` (App.vue switch layout theo `route.meta.layout`) thay vì nested 3 record cha — đã thử nested, matcher vue-router khớp `/` vào record cha AuthLayout (đúng rủi ro mục 7 của kế hoạch; test bắt được ngay). Migrate 1 trang = đổi `meta.layout` từ `'legacy'` sang `'app'`.
7. **Toast + Confirm** — `useToast()` (vue-sonner, bottom-right, 5s, `<Toaster/>` trong App.vue); `useConfirm()` promise-based + host `ConfirmDialog` (AlertDialog) đặt 1 lần ở App.vue.
8. **Vitest setupFiles** — stub ResizeObserver/matchMedia/scrollIntoView; viết lại `App.test.ts`: khóa cặp (route name → layout) cho cả 23 route + mount smoke 2 layout; ESLint override tắt 3 rule cho `src/components/ui/**`.
9. **Compat preflight** — khối compat ~60 dòng cuối `style.css` (lần sửa duy nhất được phép), scope `.app-shell`: khôi phục font-size 16px, margin/size heading, margin `p`, list-style, cursor pointer button, nền input — các default trình duyệt mà layout legacy dựa vào nhưng bị preflight reset.
10. **Migrate LoginPage + ChangePasswordPage** — Card/Input/Label/Button, label gắn for/id, lỗi API hiển thị khối destructive, đổi mật khẩu thành công báo toast; sửa logout (cả AppTopbar lẫn LegacyLayout) điều hướng về `login`.

## Ghi chú cho đợt sau

- Icon dùng package `@lucide/vue` (lucide đã đổi tên từ `lucide-vue-next`; code shadcn-vue generate import từ `@lucide/vue`). CHỈ import đích danh.
- `Sonner.vue` có đánh dấu `[đã sửa tay]`: bọc `hsl(var(--...))` vì token dự án là bộ ba HSL.
- Token bổ sung ngoài bảng gốc: `--secondary`/`--secondary-foreground` (= accent) vì button variant secondary của shadcn cần.
- Trang legacy luôn light (quyết định chính thức); ThemeToggle chỉ có ở AppLayout.

## Lệnh xác minh cuối phase

```powershell
cd src/frontend
npm run lint
npm run build
npm test
```

## Kết quả gate

Hoàn thành: 2026-07-08.

- `npm run lint`: pass, 0 error 0 warning.
- `npm run build`: pass (vue-tsc strict). Bundle: JS 428 kB (gzip 130 kB), CSS 71 kB (gzip 14 kB) — baseline để theo dõi mỗi đợt.
- `npm test`: 7/7 pass (2 file).
- Contrast checker: body text + badge soft đạt AA 4.5:1 cả light lẫn dark (sau điều chỉnh token ghi ở task 3).
- Trang chưa migrate chạy nguyên trong LegacyLayout (compat preflight đã vá); login/đổi mật khẩu dùng UI mới trong AuthLayout.
