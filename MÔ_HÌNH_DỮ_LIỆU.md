# Internal Knowledge Copilot - Data Model MVP

Ngày lập: 2026-05-09

## 1. Nguyên tắc thiết kế dữ liệu

- SQLite là nguồn sự thật cho dữ liệu nghiệp vụ.
- Qdrant chỉ lưu vector chunks và metadata phục vụ retrieval.
- File gốc lưu trong local folder, database chỉ lưu metadata/path.
- Schema hạn chế phụ thuộc tính năng riêng của SQLite để dễ migrate sau này.
- Dùng soft delete cho dữ liệu quan trọng.
- Mọi trạng thái cần rõ ràng để dashboard và audit dễ tính.

## 2. Enum chính

UserRole:

- Admin
- User
- Reviewer

DocumentStatus:

- PendingReview
- Approved
- Rejected
- Archived
- Deleted

DocumentVersionStatus:

- PendingReview
- Approved
- Rejected
- Processing
- Indexed
- ProcessingFailed

WikiStatus:

- Draft
- Published
- Rejected
- Archived

AiFeedbackValue:

- Correct
- Incorrect

FeedbackReviewStatus:

- New
- InReview
- Resolved

VisibilityScope:

- Folder
- Company

ProcessingJobStatus:

- Pending
- Running
- Succeeded
- Failed

## 3. Bảng users

Mục đích:

- Lưu tài khoản người dùng.

Trường chính:

- id
- email
- display_name
- password_hash
- role
- primary_team_id
- must_change_password
- is_active
- created_at
- updated_at
- deleted_at

Ghi chú:

- Admin tạo user thủ công.
- User đổi mật khẩu sau lần đăng nhập đầu.

## 4. Bảng teams

Mục đích:

- Lưu team/phòng ban.

Trường chính:

- id
- name
- description
- created_at
- updated_at
- deleted_at

Team MVP ban đầu:

- Kỹ thuật
- Hỗ trợ khách hàng

## 5. Bảng folders

Mục đích:

- Lưu cây thư mục tài liệu.

Trường chính:

- id
- parent_id
- name
- path
- created_by_user_id
- created_at
- updated_at
- deleted_at

Ghi chú:

- path có thể lưu dạng hiển thị, ví dụ `/Support/Payment`.
- Folder bị xóa nên dùng soft delete.

## 6. Bảng folder_permissions

Mục đích:

- Gán quyền xem folder theo team.

Trường chính:

- id
- folder_id
- team_id
- can_view
- created_at
- updated_at

Quy tắc MVP:

- User thấy folder nếu team chính hoặc quyền bổ sung cho phép.
- User không có quyền thì không thấy folder/tài liệu.

## 7. Bảng user_folder_permissions

Mục đích:

- Cấp quyền bổ sung cho user vào folder cụ thể.

Trường chính:

- id
- user_id
- folder_id
- can_view
- created_at
- updated_at

Ghi chú:

- Dùng cho trường hợp user thuộc team chính nhưng cần xem thêm folder khác.

## 8. Bảng documents

Mục đích:

- Đại diện cho một tài liệu logic, có nhiều version.

Trường chính:

- id
- folder_id
- title
- description
- status
- current_version_id
- created_by_user_id
- created_at
- updated_at
- deleted_at

Ghi chú:

- Khi upload version mới, user phải chọn document hiện có.
- Không tự đoán document theo tên file.

## 9. Bảng document_versions

Mục đích:

- Lưu từng phiên bản file của document.

Trường chính:

- id
- document_id
- version_number
- original_file_name
- stored_file_path
- file_extension
- file_size_bytes
- content_type
- status
- reject_reason
- extracted_text_path
- text_hash
- uploaded_by_user_id
- reviewed_by_user_id
- reviewed_at
- indexed_at
- created_at
- updated_at

Quy tắc:

- Version mới ở PendingReview.
- Version cũ vẫn current cho đến khi version mới approved và indexed.
- Rejected version không được index.

## 10. Bảng document_permissions

Mục đích:

- Override quyền xem ở cấp tài liệu.

Trường chính:

- id
- document_id
- user_id
- team_id
- can_view
- created_at
- updated_at

Ghi chú:

- user_id hoặc team_id có thể null tùy loại override.
- MVP chỉ dùng khi cần ngoại lệ, không biến thành ma trận quyền phức tạp.

## 11. Bảng wiki_drafts

Mục đích:

- Lưu wiki draft do AI sinh từ document approved.

Trường chính:

- id
- source_document_id
- source_document_version_id
- title
- content
- language
- status
- reject_reason
- generated_by_user_id
- reviewed_by_user_id
- created_at
- updated_at
- reviewed_at

Quy tắc:

- Draft được sinh thủ công bởi Reviewer.
- Rejected draft phải có lý do.

## 12. Bảng wiki_pages

Mục đích:

- Lưu wiki đã publish.

Trường chính:

- id
- source_draft_id
- source_document_id
- source_document_version_id
- title
- content
- language
- visibility_scope
- folder_id
- is_company_public_confirmed
- published_by_user_id
- published_at
- archived_at
- created_at
- updated_at

Quy tắc:

- Nếu visibility_scope = Company thì is_company_public_confirmed phải true.
- MVP chưa cần versioning wiki.

## 13. Bảng ai_interactions

Mục đích:

- Lưu bản ghi tối thiểu cho Q&A, feedback và KPI.

Trường chính:

- id
- user_id
- question
- answer
- scope_type
- scope_folder_id
- scope_document_id
- created_at
- latency_ms
- used_wiki_count
- used_document_count

Ghi chú:

- MVP chưa cần trang xem lại lịch sử hỏi đáp cho user.
- Bảng này vẫn cần để gắn feedback và đo KPI.

## 14. Bảng ai_interaction_sources

Mục đích:

- Lưu nguồn đã dùng trong câu trả lời.

Trường chính:

- id
- ai_interaction_id
- source_type
- source_id
- document_id
- document_version_id
- wiki_page_id
- title
- folder_path
- excerpt
- rank
- created_at

## 15. Bảng ai_feedback

Mục đích:

- Lưu feedback đúng/sai từ user.

Trường chính:

- id
- ai_interaction_id
- user_id
- value
- note
- review_status
- reviewed_by_user_id
- reviewer_note
- created_at
- updated_at
- resolved_at

Quy tắc:

- value = Incorrect tạo hàng chờ cho Reviewer.

## 16. Bảng processing_jobs

Mục đích:

- Quản lý background jobs trong .NET.

Trường chính:

- id
- job_type
- target_type
- target_id
- status
- attempts
- error_message
- created_at
- started_at
- finished_at

Job types:

- ExtractDocument
- EmbedDocument
- GenerateWikiDraft
- EmbedWiki

## 17. Bảng audit_logs

Mục đích:

- Ghi hành động nghiệp vụ chính.

Trường chính:

- id
- actor_user_id
- action
- entity_type
- entity_id
- metadata_json
- created_at

Actions MVP:

- UserCreated
- FolderCreated
- PermissionChanged
- DocumentUploaded
- DocumentApproved
- DocumentRejected
- WikiDraftGenerated
- WikiPublished
- WikiRejected
- AiFeedbackSubmitted

## 18. Qdrant collection

Collection đề xuất:

- knowledge_chunks

Vector:

- embedding vector từ AI embedding model đã chọn.

Payload:

- chunk_id
- source_type: document hoặc wiki
- source_id
- document_id
- document_version_id
- wiki_page_id
- folder_id
- team_id
- title
- folder_path
- version_number
- status
- visibility_scope
- chunk_text
- created_at

Nguyên tắc:

- Chỉ index document version approved/indexed.
- Chỉ index wiki page published.
- Khi version mới approved, chunks của version cũ có thể giữ nhưng phải filter theo current version hoặc đánh dấu inactive.
- SQLite vẫn là nguồn sự thật cho quyền.

