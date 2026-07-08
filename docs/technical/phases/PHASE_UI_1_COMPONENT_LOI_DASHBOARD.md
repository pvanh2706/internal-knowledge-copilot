# Phase UI 1 - Component Lõi + Dashboard

Ngày lập: 2026-07-08

## Mục tiêu

Đợt 1 của [KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md](../../KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md): xây bộ component nghiệp vụ lõi và migrate trang đầu tiên (Dashboard) sang AppLayout.

## Task đã thực hiện

1. **`components/app/status.ts`** — `statusInfo/statusVariant/statusLabel` sinh từ 16 union type trong `src/api` (dùng `Record<EnumType, ...>` để vue-tsc bắt thiếu/thừa case khi API đổi); label tiếng Việt; confidence chữ thường. Key trùng giữa các domain (Approved, Rejected...) thống nhất 1 label.
2. **`StatusBadge.vue`** — kiểu soft: nền alpha 0.12 (dark 0.18), chữ + border cùng token; status lạ rơi về muted + in nguyên văn; prop `label` để ghi đè.
3. **`PageHeader.vue`** — title `text-2xl font-semibold tracking-tight` + description + slot `actions`, margin-bottom 24px.
4. **`StatCard.vue`** — KPI `text-3xl font-semibold tabular-nums`, label + hint muted.
5. **`EmptyState.vue`** — icon lucide 40px `muted-foreground/50` (mặc định Inbox), title + description + slot `action`, viền dashed.
6. **`ErrorState.vue`** — khối `border-destructive/30 bg-destructive/10` + nút "Thử lại" (emit `retry`, ẩn được qua `hideRetry`).
7. **Migrate DashboardPage** — `meta.layout: 'app'`; PageHeader; 8 StatCard (grid 4/2/1 cột theo 1280/768/mobile); bảng nguồn trích dẫn dùng `ui/table` tĩnh trong Card (cuộn ngang trong container riêng); skeleton đúng hình dạng (8 ô + 1 bảng); EmptyState cho user không đủ quyền và cho bảng rỗng; ErrorState có retry; Việt hóa nốt label ("Tài liệu đã duyệt", "Wiki đã xuất bản", sourceType map Tài liệu/Wiki/Hiệu chỉnh).

## Lệnh xác minh + kết quả gate

```powershell
cd src/frontend; npm run lint; npm run build; npm test
```

Hoàn thành: 2026-07-08. Lint pass; build pass (vue-tsc strict; bundle JS 433 kB gzip 131 kB, CSS 75 kB gzip 14 kB); test 10/10 pass (thêm `StatusBadge.test.ts` — bắt được 1 xung đột label `Approved` giữa domain document và action approval, đã thống nhất "Đã duyệt").
