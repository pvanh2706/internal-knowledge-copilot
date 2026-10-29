# Enterprise AI Copilot — Project Status

**Version:** 0.1  
**Document status:** APPROVED  
**Last updated:** 2026-10-29  
**Primary repository:** pvanh2706/internal-knowledge-copilot  
**Branch:** main (target for documentation PR)

## 1. Executive Summary

Enterprise AI Copilot đang ở giai đoạn thiết lập Product Governance và chuẩn bị đánh giá kiến trúc sản phẩm B.

Các quyết định chiến lược chính đã được PO phê duyệt:

- Internal Knowledge Copilot (B) là sản phẩm chính.
- Tạm dừng mở rộng AI Operational Platform (A).
- Phát triển B thành Enterprise Context-Aware AI Copilot.
- Tích hợp Jira, CRM, ERP qua API.
- Không tái xây dựng nghiệp vụ đã được hệ thống nguồn quản lý tốt.

**Current phase:** Project Foundation — governance documentation review/merge pending.

**Implementation authorization:** Chưa cấp phép triển khai code mới.

## 2. Project Governance Status

| Công việc | Trạng thái |
|---|---|
| Project Instructions | COMPLETED |
| GitHub Source of Truth reference | COMPLETED |
| Decision Management mechanism | APPROVED |
| Conversation Handoff Protocol | COMPLETED |
| Documentation structure | APPROVED |
| PROJECT_CONTEXT.md v0.1 | CONTENT APPROVED — INCLUDED IN GATE A PR |
| PROJECT_STATUS.md v0.1 | CONTENT APPROVED — INCLUDED IN GATE A PR |
| DECISION_LOG.md v0.1 | CONTENT APPROVED — INCLUDED IN GATE A PR |
| Individual decision records DEC-0001..0003 | CONTENT APPROVED — INCLUDED IN GATE A PR |
| Repository governance documentation update | GATE A — PR REVIEW PENDING |
| Merge into main | GATE B — NOT AUTHORIZED |

COMPLETED ở đây phản ánh việc thiết lập trong ChatGPT Project; file trong Pull Request chưa được coi là đã merge vào main.

## 3. Existing Technical Baseline

Theo tài liệu hiện hành của repository B, hệ thống đã có nền tảng:

**Application**
- ASP.NET Core API.
- Vue 3, TypeScript, Vite.
- SQLite metadata database.
- ChromaDB vector store.
- Vector-store abstraction cho khả năng thay thế backend.
- Windows Server / IIS deployment documentation.

**Knowledge & AI**
- Document upload và versioning.
- Reviewer approval.
- Document processing và indexing.
- AI Q&A có citation.
- Vector và keyword retrieval.
- Wiki generation/publishing.
- Feedback, dashboard và audit log.

**Enterprise Integration Foundation**
- Tenant/application/connection model.
- Inbound integration events.
- External object metadata synchronization.
- External ACL synchronization.
- Connector boundary và permission revalidation.
- Workflow recommendation capability.

**Verification status:** DOCUMENTED — chưa thực hiện independent architecture/code verification trong Project Enterprise AI Copilot.

Không mặc định mọi tính năng đã sẵn sàng production.

## 4. Known Technical Gaps & Risks

Các vấn đề được tài liệu repository ghi nhận hoặc cần đánh giá:

| Vấn đề | Trạng thái |
|---|---|
| Chất lượng retrieval trên dữ liệu thực tế | NEEDS EVALUATION |
| Chunking và citation reliability | NEEDS EVALUATION |
| Tích hợp lấy nội dung tài liệu ngoài tự động | DOCUMENTED GAP |
| Outbound connector authentication | NEEDS REVIEW |
| External permissions và tenant isolation | NEEDS REVIEW |
| Enterprise scalability và observability | NEEDS REVIEW |
| Khả năng tái sử dụng integration foundation cho Jira/CRM | NEEDS REVIEW |

Các mục trên chưa được coi là implementation blockers chính thức cho đến khi hoàn thành review.

## 5. Discussion Workstreams

| ID | Workstream | Trạng thái |
|---|---|---|
| 00 | Product Strategy & Decisions | STRATEGY APPROVED |
| 01 | Architecture Review B | NOT STARTED |
| 02 | Hybrid RAG Improvements | NOT STARTED |
| 03 | Jira Integration | NOT STARTED |
| 04 | CRM Integration | NOT STARTED |
| 05 | Roadmap & Delivery | NOT STARTED |

NOT STARTED chỉ phản ánh các workstream mới của Enterprise AI Copilot, không phủ nhận những năng lực hoặc tài liệu đã tồn tại trong B.

## 6. Approved Decisions

Các quyết định nội dung đã được phê duyệt:

- DEC-0001 — Primary Product Strategy.
- DEC-0002 — Enterprise Context-Aware Direction.
- DEC-0003 — Source System Ownership.

Decision records đã được chuẩn bị trong Gate A PR; sau khi được merge, nguồn tra cứu chính thức trên main là `docs/decisions/DECISION_LOG.md`.

## 7. Open Questions

Những câu hỏi cần giải quyết trong các workstream tiếp theo:

1. Kiến trúc hiện tại của B có những điểm mạnh, giới hạn và technical debt nào?
2. Những năng lực nào của B có thể tái sử dụng trực tiếp cho Enterprise Context Awareness?
3. Hybrid RAG hiện tại đạt mức chất lượng nào?
4. Nên sử dụng mô hình tích hợp nào cho Jira và CRM dựa trên foundation đang có?
5. Những năng lực nào của A thực sự cần nghiên cứu để tái sử dụng?

Đây là danh sách vấn đề cần nghiên cứu, chưa phải quyết định triển khai.

## 8. Next Actions

**Current next action:** PO review Gate A documentation PR, sau đó quyết định riêng về Gate B (merge vào main).

Sau khi PO phê duyệt và cho phép merge:

- Xác minh 6 tài liệu mới và mục lục đã có trên main.
- Xác minh các liên kết và status sau merge.
- Đề xuất bắt đầu workstream `01 — Architecture Review B`.

**Proposed next workstream:** 01 — Architecture Review B.

Architecture Review phải đánh giá implementation thực tế trước khi đề xuất thay đổi kỹ thuật.

## 9. Documentation References

- `PROJECT_CONTEXT.md` — approved content, pending merge into main.
- `BÀN_GIAO_AI.md` — existing technical handoff.
- `docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md` — existing MVP roadmap.
- `docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md` — existing technical overview.
- `docs/technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md` — existing integration documentation.
- `docs/decisions/DECISION_LOG.md` — approved content, pending merge into main.

## 10. Status Maintenance Rules

- Chỉ ghi APPROVED khi có xác nhận của PO.
- Chỉ ghi VERIFIED khi có bằng chứng kiểm tra phù hợp.
- Cập nhật trạng thái khi hoàn thành một milestone hoặc có thay đổi quan trọng.
- Không sao chép chi tiết roadmap hoặc technical design vào file này.
- Khi tài liệu hoặc trạng thái implementation mâu thuẫn, phải báo PO.
- Không tự động chuyển từ APPROVED sang IMPLEMENTED.
- Mọi thay đổi repository cần được PO cho phép.
