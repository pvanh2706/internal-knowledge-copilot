# Enterprise AI Copilot — Project Context

**Version:** 0.2  
**Document status:** APPROVED  
**Last reviewed:** 2026-10-29  
**Product Owner:** Project owner

## 1. Project Identity

**Project:** Enterprise AI Copilot

**Primary repository:**  
https://github.com/pvanh2706/internal-knowledge-copilot

**Reference repository (paused):**  
https://github.com/pvanh2706/ai-operational-platform

Internal Knowledge Copilot (B) là sản phẩm chính, được định hướng phát triển thành Enterprise Context-Aware AI Copilot.

Repository A chỉ được sử dụng làm nguồn nghiên cứu và tham khảo những năng lực chuyên sâu có thể tái sử dụng.

## 2. Product Vision

Xây dựng AI Copilot giúp nhân viên doanh nghiệp truy cập tri thức nội bộ và nhận hỗ trợ theo ngữ cảnh công việc thực tế.

Copilot kết hợp:

- Enterprise Knowledge Management.
- Hybrid RAG và AI Q&A có citation.
- Context Awareness từ các hệ thống nghiệp vụ.
- Kết nối Jira, CRM, ERP và các hệ thống doanh nghiệp qua API.
- AI Assistance dựa trên tri thức và ngữ cảnh có thẩm quyền.

Copilot hỗ trợ các hệ thống nghiệp vụ hiện có, không nhằm thay thế chúng.

## 3. Approved Product Decisions

Các quyết định dưới đây đã được PO xác nhận trong quá trình thảo luận và được ghi trong Decision Log.

### DEC-0001 — Primary Product Strategy

Status: APPROVED

- Internal Knowledge Copilot (B) là sản phẩm chính.
- Tạm dừng mở rộng AI Operational Platform (A).
- Mọi phát triển mới ưu tiên dựa trên B.

### DEC-0002 — Enterprise Context-Aware Direction

Status: APPROVED

- Phát triển B thành Enterprise Context-Aware AI Copilot.
- Hỗ trợ kết nối Jira, CRM, ERP và các hệ thống doanh nghiệp qua API.
- Tận dụng tri thức, dữ liệu và ngữ cảnh của hệ thống nguồn.

### DEC-0003 — Source System Ownership

Status: APPROVED

- Không xây lại Case, Workflow hoặc Process nếu hệ thống nguồn đã quản lý tốt.
- Hệ thống nguồn tiếp tục chịu trách nhiệm đối với nghiệp vụ do chính nó quản lý.
- Chỉ tái sử dụng năng lực chuyên sâu của A khi có nhu cầu thực tế và được PO chấp thuận.

### Newly Approved Enterprise Architecture Decisions

PO approved 2026-10-29; decisions DEC-0004..07 published to main through PR #3:

- [DEC-0004](docs/decisions/DEC-0004.md) — Strict Security Boundary (Option A, fail-closed/read-only initially).
- [DEC-0005](docs/decisions/DEC-0005.md) — Platform Admin and Tenant Admin separation (D1=B).
- [DEC-0006](docs/decisions/DEC-0006.md) — Centralized Authorization/Revalidation (D2=B).
- [DEC-0007](docs/decisions/DEC-0007.md) — Incremental Enterprise Hardening (D3=B).

[AR-B-01..AR-B-04 Architecture Review](docs/architecture/AR-B-01_TO_AR-B-04_ARCHITECTURE_REVIEW.md) đã được PO phê duyệt. Findings SEC-01..07 và TD-01..06 còn OPEN; S01..05 chỉ là preliminary approved planning, không có implementation permission.

Tham khảo: [Decision Log](docs/decisions/DECISION_LOG.md).

## 4. Existing Product Baseline

Theo tài liệu hiện hành trong repository B, sản phẩm đã có các thành phần:

**Application**
- Backend: ASP.NET Core API.
- Frontend: Vue 3, TypeScript, Vite.
- Metadata database: SQLite.
- Vector store hiện tại: ChromaDB.
- Vector store alternative: Qdrant qua abstraction.
- Deployment target: Windows Server / IIS.

**Knowledge & AI**
- Document upload, versioning và reviewer approval.
- Document processing, chunking, embedding, indexing.
- Vector và keyword retrieval.
- AI Q&A có citation.
- Wiki draft và publishing.
- Feedback, dashboard, audit log.

**Integration Foundation**
- Tenant/application/connection model.
- External object metadata synchronization.
- External ACL synchronization.
- Inbound integration events.
- Connector boundary cho context/permission revalidation.
- Workflow recommendation capabilities.

Đây là baseline dựa trên tài liệu repository, không phải xác nhận rằng tất cả chức năng đã vượt qua kiểm thử production.

Mức độ sẵn sàng cho Jira, CRM và ERP cụ thể phải được đánh giá riêng.

## 5. Architectural Boundaries

### Internal Knowledge Copilot Owns

- Knowledge ingestion và quản trị tri thức nội bộ.
- Knowledge retrieval và citation.
- AI assistance và answer generation.
- Quản lý các tài liệu, wiki và metadata thuộc phạm vi Copilot.
- Tích hợp và sử dụng ngữ cảnh nghiệp vụ theo thiết kế được phê duyệt.

### Source Systems Own

- Business records và business state.
- Case/Issue/Deal lifecycle khi đã được quản lý tại hệ thống nguồn.
- Workflow execution và business actions thuộc trách nhiệm hệ thống nguồn.
- Các quyền nghiệp vụ và chính sách truy cập có thẩm quyền tại hệ thống nguồn.

Các cơ chế kiểm tra quyền và đồng bộ quyền phải được đánh giá phù hợp với yêu cầu bảo mật từng tích hợp.

Không mặc định Copilot được phép ghi dữ liệu hoặc thực hiện action trên hệ thống nguồn.

## 6. Governance

**Product Owner**
- Quyết định chiến lược, phạm vi và ưu tiên.
- Phê duyệt kiến trúc và các quyết định quan trọng.
- Cho phép triển khai và nghiệm thu.

**ChatGPT**
- Product Architect.
- Business Analyst.
- Technical Reviewer.
- Chuẩn bị phân tích, thiết kế, đánh giá và prompt cho coding agent.

**Coding Agents**
- Chỉ thực hiện nhiệm vụ được PO cho phép.
- Tuân thủ phạm vi, tiêu chí nghiệm thu và quy tắc kỹ thuật.

## 7. Discussion Workstreams

- 00 — Product Strategy & Decisions.
- 01 — Architecture Review B.
- 02 — Hybrid RAG Improvements.
- 03 — Jira Integration.
- 04 — CRM Integration.
- 05 — Roadmap & Delivery.

Mỗi cuộc trò chuyện tập trung vào một chủ đề.

## 8. Decision & Change Management

Decision status:
- PROPOSED.
- APPROVED.
- SUPERSEDED.

Quyết định được PO phê duyệt trong hội thoại cần được lưu thành tài liệu version-controlled khi được phép cập nhật repository.

Không tự ý:
- Viết code.
- Sửa repository.
- Commit/push.
- Thay đổi kiến trúc quan trọng.
- Mở rộng phạm vi sản phẩm.

Khi có xung đột giữa quyết định mới, tài liệu cũ và implementation, phải xác minh và báo PO.

## 9. Documentation Governance

**Primary source of truth:** GitHub B.

Tài liệu hiện hành cần tham khảo:

- `GIỚI_THIỆU.md`
- `BÀN_GIAO_AI.md`
- `KIẾN_TRÚC_MVP.md`
- `docs/MỤC_LỤC_TÀI_LIỆU.md`
- `docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md`
- `docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md`
- `docs/technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md`

Tài liệu quản trị dự án:

- `PROJECT_STATUS.md`
- `docs/decisions/DECISION_LOG.md`
- `docs/decisions/DEC-xxxx.md`

Không coi ChatGPT Memory hoặc lịch sử trò chuyện là bằng chứng về trạng thái code mới nhất.

## 10. Next Planning Priorities

1. Workstream 01 Architecture Review B: **COMPLETED / APPROVED** (AR-B-01..04), published through PR #3.
2. **S01 Technical Design Baseline v1.0 APPROVED** by PO 2026-10-30; [S01 decision record](docs/decisions/DEC-0008.md) and [design baseline](docs/architecture/S01_TECHNICAL_DESIGN_BASELINE_V1.0.md) are proposed for documentation publication in a separate PR; S01 implementation is **NOT AUTHORIZED**.
3. Complete documentation PR review/PO-approved merge, then prepare a separately authorized S01 Implementation Gate. Security findings remain OPEN until runtime verification.
4. Nghiên cứu riêng Hybrid RAG, Jira, CRM và Roadmap trong các workstreams tương ứng.
5. Benchmark trước khi quyết định DB/queue/storage migration; không mặc định microservices.

**Implementation authorization: NOT AUTHORIZED.** Các findings chưa được khắc phục hay kiểm chứng production.
