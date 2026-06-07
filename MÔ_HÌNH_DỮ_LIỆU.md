# Internal Knowledge Copilot - Data Model

Ngày cập nhật: 2026-06-07

Tài liệu này là bản tóm tắt mô hình dữ liệu hiện tại. Danh sách bảng đầy đủ và phần đối chiếu với code nằm tại [docs/technical/DANH_SACH_BANG_DU_LIEU_HE_THONG.md](docs/technical/DANH_SACH_BANG_DU_LIEU_HE_THONG.md).

## Nguyên Tắc

- SQLite là source of truth cho dữ liệu nghiệp vụ, quyền, trạng thái tài liệu, wiki, feedback, audit và cấu hình AI.
- ChromaDB là vector store hiện tại cho semantic retrieval. Qdrant chỉ là future option sau boundary `IKnowledgeVectorStore`.
- Vector metadata chỉ dùng để filter nhanh. Backend vẫn phải recheck quyền và trạng thái nguồn bằng SQLite trước khi đưa chunk vào prompt.
- File gốc, extracted text và normalized text lưu trong local storage; database chỉ lưu metadata/path.
- Schema nên giữ hướng dễ migrate sang SQL Server/PostgreSQL khi cần sản phẩm hóa.

## Nhóm Bảng Chính

| Nhóm | Bảng |
| --- | --- |
| User và phân quyền | `users`, `teams`, `folders`, `folder_permissions`, `user_folder_permissions` |
| Tài liệu và xử lý nền | `documents`, `document_versions`, `processing_jobs` |
| Hỏi đáp AI và citation | `ai_interactions`, `ai_interaction_sources` |
| Feedback và cải thiện AI | `ai_feedback`, `ai_quality_issues`, `knowledge_corrections`, `retrieval_hints` |
| Knowledge index | `knowledge_chunks`, `knowledge_chunk_indexes` |
| Evaluation | `evaluation_cases`, `evaluation_runs`, `evaluation_run_results` |
| Wiki | `wiki_drafts`, `wiki_pages` |
| Audit và cấu hình | `audit_logs`, `ai_provider_settings` |

## Lưu Ý Về Quyền

Schema hiện tại không có bảng `document_permissions`. Quyền xem tài liệu đang được enforce theo folder/team/user:

- `folder_permissions`: cấp quyền theo team.
- `user_folder_permissions`: cấp quyền bổ sung theo user.
- `documents.folder_id`: tài liệu thừa hưởng quyền từ folder.

Nếu sau này cần override quyền cấp document, hãy thiết kế feature riêng và cập nhật cả service permission, retrieval filter, tests và tài liệu này.

## Lưu Ý Về Vector Store

Chroma collection hiện tại:

- `knowledge_chunks`

Payload quan trọng:

- `chunk_id`
- `source_type`: `document`, `wiki`, hoặc `correction`
- `source_id`
- `document_id`
- `document_version_id`
- `wiki_page_id`
- `folder_id`
- `visibility_scope`
- `tenant_id`
- `application_id`
- `knowledge_source_id`
- `external_object_type`
- `external_object_id`
- `status`
- `title`
- `section_title`
- `chunk_index`

Bảng SQLite `knowledge_chunks` là ledger/metadata để đối soát và rebuild. Bảng này không phải nơi lưu vector embedding.

## Luồng Quan Hệ Cốt Lõi

- User -> team -> folder permission -> document visibility.
- Document -> document versions -> processing jobs -> knowledge chunks.
- Wiki draft -> wiki page -> knowledge chunks.
- AI interaction -> interaction sources -> feedback/quality issue/correction.
- Evaluation case -> evaluation run -> evaluation result.

## Source Of Truth Chi Tiết

Dùng các tài liệu sau khi cần chi tiết hơn:

- [Danh sách bảng dữ liệu hệ thống](docs/technical/DANH_SACH_BANG_DU_LIEU_HE_THONG.md)
- [Luồng upload tài liệu thành tri thức](docs/technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md)
- [Luồng hỏi đáp AI](docs/technical/LUỒNG_HỎI_ĐÁP_AI.md)
