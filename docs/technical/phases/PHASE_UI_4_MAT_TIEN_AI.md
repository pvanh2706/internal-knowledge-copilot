# Phase UI 4 - Mặt Tiền AI

Ngày lập: 2026-07-08

## Mục tiêu

Đợt 4 của [KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md](../../KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md): màn hình demo bán hàng AI Q&A theo đặc tả mục 4, cùng RetrievalExplain và WikiDraft.

## Task đã thực hiện

1. **`CitationCard.vue`** — số `[n]` ô vuông primary-soft, icon theo sourceType (Document/Wiki/Correction), title 1 dòng, folder path + section `text-xs font-mono`, excerpt `line-clamp-3`, prop `meta` cho dòng score/distance (RetrievalExplain). V1 không click-through (source viewer trong backlog).
2. **`ScopeSelector.vue`** — chip Popover chọn Tất cả/Thư mục/Tài liệu, chip có icon + tên + nút x reset về Tất cả; dùng chung AiQuestionPage + RetrievalExplainPage (trước đây trùng 100% giữa 2 trang).
3. **`AnswerPanel.vue`** — badge confidence map token (high→success "Độ tin cậy cao", medium→warning, low→destructive), nút copy (toast), thân `text-base leading-7 whitespace-pre-wrap` (bỏ thẻ `pre` cũ), callout info khi `needsClarification`, callout viền trái 2px warning (thiếu thông tin) / destructive (mâu thuẫn), slot follow-up chips, feedback 👍/👎 — 👎 mở khung note, kết quả báo toast.
4. **AiQuestionPage** (L, spec mục 4) — cột đơn `max-w-3xl` căn giữa; prompt box card `rounded-xl` focus-within ring, textarea không viền auto-grow 2→8 dòng, Enter gửi / Shift+Enter xuống dòng, scope chip trái + nút gửi primary phải; loading skeleton 4 dòng + "Đang tìm trong kho tri thức..."; citations grid 2 cột (mobile 1); follow-up chip pill click điền + gửi lại; transition opacity/translate-y 150ms khi answer xuất hiện; empty state gợi ý câu hỏi mẫu.
5. **RetrievalExplainPage** — tái dùng ScopeSelector/CitationCard (meta hiển thị `retrievalSource · score · distance` dạng mono); 6 StatCard; card Truy vấn (chuẩn hóa/từ khóa/bộ lọc); bảng toàn bộ ứng viên bằng DataTable (badge quyết định theo `selectedForContext`).
6. **WikiDraftPage** — master–detail; bảng draft StatusBadge; panel: tài liệu liên quan, callout thiếu thông tin, ContentViewer cuộn `max-h-[520px] text-base leading-7`; form xuất bản có **checkbox xác nhận bắt buộc khi chọn phạm vi Company** (nút disable nếu chưa tick); **confirm trước khi từ chối** draft; toast thay message inline.

## Kết quả gate

Hoàn thành: 2026-07-08. Lint pass; build pass (vue-tsc strict); test 16/16 pass. Luồng hỏi → feedback cần demo chạy thật với backend khi nghiệm thu (chữ ký API giữ nguyên).
