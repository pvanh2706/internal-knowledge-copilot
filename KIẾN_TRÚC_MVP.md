# Internal Knowledge Copilot - Kiến Trúc MVP

Ngày cập nhật: 2026-06-07

Tài liệu này là bản kiến trúc gọn, phản ánh implementation hiện tại. Nếu cần flow chi tiết, đọc [docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md](docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md).

## Mục Tiêu

Internal Knowledge Copilot giúp quản lý tri thức nội bộ theo luồng có kiểm soát:

1. User upload tài liệu vào folder được cấp quyền.
2. Reviewer approve/reject.
3. Background job extract, normalize, chunk, embed và index.
4. User hỏi AI trên nguồn đã duyệt, có citation.
5. Reviewer xử lý feedback và publish wiki để chuẩn hóa tri thức.

## Stack Hiện Tại

| Layer | Công nghệ |
| --- | --- |
| Frontend | Vue 3, TypeScript, Vite |
| Backend | ASP.NET Core API |
| Metadata DB | SQLite qua EF Core |
| Vector store | ChromaDB sau boundary `IKnowledgeVectorStore` |
| File storage | Local filesystem ngoài public web root |
| Background processing | Hosted service + `processing_jobs` |
| Deploy target | Windows Server / IIS |

Qdrant là định hướng/future option ban đầu, không phải runtime hiện tại. Code mới nên phụ thuộc vào `IKnowledgeVectorStore`, không hard-code vào ChromaDB hay Qdrant trong business flow.

## Kiến Trúc Tổng Quan

```text
Vue UI
  -> ASP.NET Core API
      -> SQLite
      -> Local File Storage
      -> Background Processing
      -> Vector Store adapter
          -> ChromaDB hiện tại
      -> AI Provider
```

SQLite là source of truth cho user, quyền, tài liệu, wiki, feedback, audit, evaluation và cấu hình AI. Vector store chỉ phục vụ retrieval nhanh bằng embedding và metadata filter.

## Module Chính

| Module | Trách nhiệm |
| --- | --- |
| Auth/User/Role | Đăng nhập, JWT, user, role Admin/User/Reviewer |
| Team/Folder/Permission | Cây folder và quyền xem theo team/user |
| Document/Version/Approval | Upload, versioning, approve/reject, current version |
| Document Processing | Extract text, normalize, detect sections, chunk, embedding, index |
| AI Q&A | Retrieval, rerank, context packing, gọi LLM, citation, interaction log |
| Wiki | Generate draft từ document indexed, reviewer publish/reject, index wiki |
| Feedback/Quality | User feedback, reviewer queue, correction, retrieval hint |
| Dashboard/Audit | KPI, audit log, operational trace |
| Integration/Workflow | Tenant/application, external objects, ACL snapshot, CRM workflow recommendation |

## Luồng Tài Liệu Thành Tri Thức

```text
Upload file
-> validate file và quyền folder
-> lưu file vào local storage
-> lưu Document/DocumentVersion vào SQLite với PendingReview
-> Reviewer approve
-> tạo processing job
-> extract/normalize/section/chunk
-> tạo embedding
-> upsert vào ChromaDB qua IKnowledgeVectorStore
-> ghi ledger SQLite knowledge_chunks và keyword index
-> DocumentVersion = Indexed
```

Chỉ version `Indexed` và là current version mới được dùng cho AI Q&A.

## Luồng AI Q&A

```text
User đặt câu hỏi
-> resolve tenant/user/scope
-> tạo query embedding
-> truy vấn vector store + keyword index
-> filter và recheck quyền/trạng thái bằng SQLite
-> rerank và pack context
-> gọi AI provider
-> trả answer + citations
-> lưu ai_interactions và ai_interaction_sources
```

Nguyên tắc an toàn: không đưa chunk vào prompt nếu backend chưa xác nhận user có quyền với nguồn đó.

## Vai Trò Của Wiki

Wiki là lớp tri thức đã được reviewer publish, dùng để giảm phụ thuộc vào tài liệu gốc dài, rời rạc hoặc trùng lặp.

```text
Document Indexed
-> Generate wiki draft
-> Reviewer publish
-> Wiki page được chunk/embed/index
-> AI Q&A ưu tiên wiki khi phù hợp
```

## Quyền Và Bảo Mật

- Folder permission và user folder permission là cơ chế quyền chính hiện tại.
- Không có bảng `document_permissions` trong schema hiện tại.
- Download file phải đi qua API có authorization.
- File upload không nằm trong public web root.
- Vector metadata không phải enforcement layer duy nhất.
- Company-wide wiki cần reviewer xác nhận nội dung được phép public nội bộ.
- Secrets/API keys không được commit vào source code.

## Background Processing

MVP dùng hosted service và bảng `processing_jobs`. Khi workload lớn hơn có thể tách worker riêng hoặc dùng queue/Hangfire, nhưng chưa cần cho scope hiện tại.

Job hiện tại gồm xử lý tài liệu, index/rebuild tri thức, sync external object/permission, action/workflow hardening theo các phase đã implement.

## Deploy Và Backup

- Vue build thành static files.
- ASP.NET Core API chạy sau IIS.
- SQLite và local storage cần backup cùng nhau.
- ChromaDB có thể rebuild từ document/wiki/correction đã indexed và ledger SQLite khi cần.
- Không expose ChromaDB trực tiếp cho end user.

## Ngoài Phạm Vi MVP

- SSO enterprise đầy đủ.
- Mobile app.
- Elasticsearch/OpenSearch.
- Redis/RabbitMQ bắt buộc.
- Virus scanning/OCR nâng cao.
- Full document-level permission override.
- Fine-tuning riêng.

## Tài Liệu Liên Quan

- [Technical system overview](docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md)
- [Data model](MÔ_HÌNH_DỮ_LIỆU.md)
- [API spec](ĐẶC_TẢ_API.md)
- [Luồng upload tài liệu thành tri thức](docs/technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md)
- [Luồng hỏi đáp AI](docs/technical/LUỒNG_HỎI_ĐÁP_AI.md)
