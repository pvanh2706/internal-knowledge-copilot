# Danh sách bảng dữ liệu hệ thống hiện tại

Ngày cập nhật: 2026-05-28

Tài liệu này liệt kê các bảng đang được khai báo trong `AppDbContext` của backend hiện tại và mô tả tác dụng của từng bảng trong hệ thống Internal Knowledge Copilot.

Nguồn đối chiếu chính:

- `src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Database/AppDbContext.cs`
- `src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Database/Entities`
- `src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Database/Migrations/AppDbContextModelSnapshot.cs`

## Ghi chú phạm vi

- Các bảng dưới đây là bảng nghiệp vụ và cấu hình trong SQLite do EF Core quản lý.
- EF Core còn tạo bảng kỹ thuật `__EFMigrationsHistory` trong database thật để theo dõi migration đã chạy. Bảng này không nằm trong `AppDbContext` và không chứa dữ liệu nghiệp vụ.
- Vector embedding được lưu ở Chroma collection, không phải bảng SQLite. Collection đang dùng là `knowledge_chunks`; bảng SQLite `knowledge_chunks` chỉ là ledger/metadata để đối soát và rebuild.
- Tài liệu cũ có nhắc `document_permissions`, nhưng schema hiện tại không khai báo bảng này. Quyền xem hiện đang quản lý ở cấp folder/team/user qua `folder_permissions` và `user_folder_permissions`.

## Tổng quan nhóm bảng

| Nhóm | Bảng |
| --- | --- |
| Người dùng và phân quyền | `users`, `teams`, `folders`, `folder_permissions`, `user_folder_permissions` |
| Tài liệu và xử lý nền | `documents`, `document_versions`, `processing_jobs` |
| Hỏi đáp AI và nguồn trích dẫn | `ai_interactions`, `ai_interaction_sources` |
| Feedback và cải thiện chất lượng AI | `ai_feedback`, `ai_quality_issues`, `knowledge_corrections`, `retrieval_hints` |
| Kho tri thức và chỉ mục tìm kiếm | `knowledge_chunks`, `knowledge_chunk_indexes` |
| Evaluation | `evaluation_cases`, `evaluation_runs`, `evaluation_run_results` |
| Wiki | `wiki_drafts`, `wiki_pages` |
| Audit và cấu hình hệ thống | `audit_logs`, `ai_provider_settings` |

## Chi tiết từng bảng

| STT | Bảng | Entity | Tác dụng |
| --- | --- | --- | --- |
| 1 | `teams` | `TeamEntity` | Lưu team/phòng ban trong tổ chức. Team được dùng để gán user và cấp quyền xem folder theo nhóm. |
| 2 | `users` | `UserEntity` | Lưu tài khoản đăng nhập, email, tên hiển thị, password hash, role, trạng thái active và team chính. Đây là bảng gốc cho xác thực, phân quyền role và audit actor. |
| 3 | `folders` | `FolderEntity` | Lưu cây thư mục tài liệu bằng quan hệ cha-con và `path`. Folder là đơn vị chính để tổ chức tài liệu và giới hạn phạm vi truy cập. |
| 4 | `folder_permissions` | `FolderPermissionEntity` | Lưu quyền xem folder theo team. Mỗi cặp `folder_id` + `team_id` là duy nhất, giúp user trong team được nhìn thấy tài liệu thuộc folder tương ứng. |
| 5 | `user_folder_permissions` | `UserFolderPermissionEntity` | Lưu quyền xem folder trực tiếp cho từng user. Bảng này dùng cho ngoại lệ hoặc quyền bổ sung ngoài quyền theo team. |
| 6 | `documents` | `DocumentEntity` | Lưu bản ghi tài liệu logic, gồm folder, tiêu đề, mô tả, trạng thái, user tạo và version hiện hành. Một document có thể có nhiều version file. |
| 7 | `document_versions` | `DocumentVersionEntity` | Lưu từng phiên bản file của document: tên file gốc, đường dẫn lưu file, trạng thái review/index, text đã extract/normalize, metadata hiểu tài liệu, người upload/review và thời điểm indexed. |
| 8 | `processing_jobs` | `ProcessingJobEntity` | Lưu job xử lý nền như extract text, phân tích tài liệu, embedding/index tài liệu. Bảng này giúp worker theo dõi trạng thái, số lần retry và lỗi xử lý. |
| 9 | `ai_interactions` | `AiInteractionEntity` | Lưu lịch sử mỗi lượt hỏi đáp AI: user hỏi, câu trả lời, scope truy vấn, độ tin cậy, thông tin thiếu, xung đột, follow-up, latency và số nguồn document/wiki đã dùng. |
| 10 | `ai_interaction_sources` | `AiInteractionSourceEntity` | Lưu các nguồn tri thức được dùng cho một câu trả lời AI. Mỗi dòng chứa loại nguồn, document/wiki liên quan, tiêu đề, folder, section, excerpt và thứ hạng retrieval. |
| 11 | `ai_feedback` | `AiFeedbackEntity` | Lưu feedback đúng/sai của user cho câu trả lời AI, ghi chú của user, trạng thái review và ghi chú reviewer. Mỗi user chỉ có một feedback cho một interaction. |
| 12 | `ai_quality_issues` | `AiQualityIssueEntity` | Lưu issue chất lượng phát sinh từ feedback AI, gồm loại lỗi, mức độ nghiêm trọng, giả thuyết nguyên nhân, evidence và hành động đề xuất. Đây là hàng đợi để reviewer phân loại và xử lý lỗi câu trả lời. |
| 13 | `knowledge_corrections` | `KnowledgeCorrectionEntity` | Lưu nội dung correction/tri thức sửa sai sau khi review issue AI. Correction có scope theo folder hoặc company, có trạng thái duyệt, người tạo, người approve và thời điểm index vào kho tri thức. |
| 14 | `retrieval_hints` | `RetrievalHintEntity` | Lưu các hint truy xuất gắn với một correction. Hint giúp cải thiện khả năng tìm đúng correction khi user hỏi lại các câu tương tự. |
| 15 | `knowledge_chunks` | `KnowledgeChunkEntity` | Lưu ledger metadata của các chunk tri thức đã index vào vector store. Bảng này ghi nguồn chunk là document/wiki/correction, quyền nhìn thấy, trạng thái, text, hash, vector id và metadata JSON để đối soát hoặc rebuild. |
| 16 | `knowledge_chunk_indexes` | `KnowledgeChunkIndexEntity` | Lưu chỉ mục text đã chuẩn hóa cho keyword search nội bộ. Bảng này hỗ trợ tìm kiếm theo từ khóa song song với vector search. |
| 17 | `evaluation_cases` | `EvaluationCaseEntity` | Lưu test case đánh giá AI: câu hỏi, expected answer, keyword mong đợi, scope chạy test và trạng thái active. Có thể được tạo từ feedback đã review. |
| 18 | `evaluation_runs` | `EvaluationRunEntity` | Lưu một lần chạy bộ evaluation, gồm tên run, tổng số case, số case pass/fail, người chạy và thời điểm hoàn tất. |
| 19 | `evaluation_run_results` | `EvaluationRunResultEntity` | Lưu kết quả từng case trong một evaluation run: câu trả lời thực tế, pass/fail, điểm số, lý do fail và interaction AI phát sinh nếu có. |
| 20 | `wiki_drafts` | `WikiDraftEntity` | Lưu bản nháp wiki sinh từ document đã indexed. Draft chứa nội dung, ngôn ngữ, thông tin còn thiếu, tài liệu liên quan, trạng thái draft/published/rejected và người generate/review. |
| 21 | `wiki_pages` | `WikiPageEntity` | Lưu wiki đã publish từ draft. Bảng này là nguồn tri thức chính thức có thể được index vào Q&A, có visibility theo folder hoặc toàn công ty và thông tin người publish. |
| 22 | `audit_logs` | `AuditLogEntity` | Lưu nhật ký hành động nghiệp vụ quan trọng: actor, action, entity type, entity id, metadata JSON và thời điểm tạo. Dùng cho truy vết thay đổi và kiểm tra vận hành. |
| 23 | `ai_provider_settings` | `AiProviderSettingEntity` | Lưu cấu hình runtime cho LLM và embedding provider: provider name, base URL, API key, header, model, endpoint mode, reasoning/temperature/token/timeout. Bảng này cho phép admin đổi cấu hình AI mà không cần sửa code. |

## Quan hệ chính giữa các bảng

| Luồng | Quan hệ dữ liệu |
| --- | --- |
| User/team/folder | `users.primary_team_id` trỏ tới `teams`; `folder_permissions` nối `folders` với `teams`; `user_folder_permissions` nối `users` với `folders`. |
| Tài liệu | `documents.folder_id` trỏ tới `folders`; `document_versions.document_id` trỏ tới `documents`; `documents.current_version_id` lưu version hiện hành. |
| Xử lý tài liệu thành tri thức | `document_versions` lưu file/text/metadata; sau khi approved/indexed, chunk được ghi vào Chroma và ledger ở `knowledge_chunks`, đồng thời keyword index ghi vào `knowledge_chunk_indexes`. |
| Hỏi đáp AI | `ai_interactions.user_id` trỏ tới `users`; `ai_interaction_sources.ai_interaction_id` trỏ tới `ai_interactions` và lưu nguồn document/wiki/correction đã dùng. |
| Feedback cải thiện AI | `ai_feedback` trỏ tới `ai_interactions`; `ai_quality_issues` trỏ tới `ai_feedback`; `knowledge_corrections` trỏ tới issue/feedback/interaction; `retrieval_hints` trỏ tới correction. |
| Wiki | `wiki_drafts` trỏ tới document và document version nguồn; `wiki_pages` trỏ tới draft đã publish, document nguồn, version nguồn, folder visibility và user publish. |
| Evaluation | `evaluation_cases` có thể trỏ tới feedback nguồn; `evaluation_runs` gom nhiều kết quả; `evaluation_run_results` nối run với case và interaction phát sinh. |
| Audit/config | `audit_logs.actor_user_id` trỏ tới user thực hiện hành động; `ai_provider_settings.updated_by_user_id` trỏ tới admin cập nhật cấu hình. |

## Bảng kỹ thuật và lưu trữ ngoài SQLite

| Thành phần | Loại | Tác dụng |
| --- | --- | --- |
| `__EFMigrationsHistory` | Bảng kỹ thuật EF Core | Theo dõi migration đã apply vào SQLite database. Không chứa dữ liệu nghiệp vụ và không khai báo trong `AppDbContext`. |
| `knowledge_chunks` trong Chroma | Vector collection | Lưu embedding vector và metadata phục vụ semantic retrieval. Đây không phải bảng SQLite, dù tên collection trùng với ledger table `knowledge_chunks`. |
| File storage local | Thư mục file | Lưu file gốc, text extract và text normalize. Database chỉ lưu đường dẫn trong `document_versions`. |

