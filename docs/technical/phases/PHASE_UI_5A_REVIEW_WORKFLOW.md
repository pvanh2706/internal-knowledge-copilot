# Phase UI 5a - Review Nâng Cao + Workflow

Ngày lập: 2026-07-08

## Mục tiêu

Đợt 5a của [KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md](../../KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md): JsonViewer + 5 trang review nâng cao/workflow, Việt hóa 3 trang tiếng Anh còn lại của nhóm.

## Task đã thực hiện

1. **`JsonViewer.vue`** (mini-spec trong kế hoạch) — pretty-print JSON hợp lệ (fallback chuỗi thô), khối `pre` scroll `max-h-64` nền muted font mono, nút copy + toast; dùng cho 4 khối JSON của ActionQueue + metadata KnowledgeSource.
2. **RecommendationListPage** (Việt hóa) — FilterBar application/loại đối tượng/mã đối tượng (giữ logic đọc query string route cũ); DataTable lịch sử; panel chi tiết: 4 khối danh sách (bước tiếp theo/rủi ro/câu hỏi/tín hiệu) chỉ hiện khi có dữ liệu, citations dùng CitationCard, form phản hồi (Hữu ích/Không hữu ích/Cần xem lại) + form tạo yêu cầu hành động (payload JSON font mono); toast thay message inline.
3. **ActionApprovalQueuePage** (Việt hóa) — filter 5 trường; **action bar theo state machine 8 trạng thái** (canApprove/canReject/canExecute giữ nguyên logic); 4 khối JSON qua JsonViewer; **confirm trước Từ chối và Thực thi** (thực thi chạm dữ liệu hệ thống nguồn); lý do từ chối bắt buộc báo toast.
4. **KnowledgeSourcePage** (Việt hóa) — 2 DataTable nguồn/đối tượng ngoài cạnh nhau (xl 2 cột), filter application + nút bỏ lọc theo nguồn, panel chi tiết dl + JsonViewer metadata, lỗi đồng bộ tô destructive.
5. **EvaluationPage** — form chạy eval trong FilterBar; 3 DataTable (case có nút Chạy từng case, lần chạy click-để-chọn, kết quả với badge Pass/Fail); kết quả chạy báo toast.
6. **KnowledgeIndexPage** — 3 StatCard + form rebuild; checkbox reset vector đóng khung destructive; **confirm bắt buộc khi reset vector store**; bảng dl kết quả lần rebuild gần nhất.
7. Cập nhật `Phase8Pages.test.ts` cùng commit: 3 assertion tiếng Anh → tiếng Việt (Gợi ý quy trình / Hàng đợi hành động / Nguồn tri thức).

## Kết quả gate

Hoàn thành: 2026-07-08. Lint pass; build pass (vue-tsc strict); test 16/16 pass. Luồng recommendation → action request → approve/execute cần demo chạy thật với backend khi nghiệm thu (chữ ký API giữ nguyên).
