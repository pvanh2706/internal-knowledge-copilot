# Tổng Quan Dự Án, Trạng Thái Và Roadmap

Tài liệu này là tài liệu điều phối chính của dự án Internal Knowledge Copilot. Nội dung đã gộp từ khảo sát yêu cầu, kế hoạch triển khai MVP, tiêu chí hoàn thành, trạng thái phase, giới hạn hiện tại và roadmap.

## 1. Mục Tiêu Sản Phẩm

Internal Knowledge Copilot giúp công ty tập trung tài liệu nội bộ, cho nhân viên hỏi đáp bằng tiếng Việt trên tài liệu đã duyệt, có nguồn trích dẫn rõ ràng và có reviewer kiểm soát chất lượng tri thức.

Mục tiêu MVP:

- Tập trung tài liệu nội bộ quan trọng vào một hệ thống có phân quyền.
- Cho user hỏi AI trên tài liệu/wiki mà họ có quyền truy cập.
- Yêu cầu câu trả lời có citation để kiểm chứng.
- Có workflow reviewer approve/reject tài liệu trước khi đưa vào AI.
- Cho phép reviewer tạo và publish wiki từ tài liệu gốc.
- Có feedback, dashboard và audit log để đo chất lượng trong pilot.

## 2. KPI Thành Công

KPI nên theo dõi trong pilot:

- Số câu hỏi AI theo ngày/tuần.
- Số user active.
- Số tài liệu upload.
- Số tài liệu approved/rejected.
- Số wiki draft generated.
- Số wiki published.
- Số feedback Correct/Incorrect.
- Số feedback sai đã xử lý.
- Top cited sources.
- Thời gian trung bình từ upload đến approve.

Pilot được xem là có tín hiệu tốt nếu:

- User đặt câu hỏi thực tế hằng tuần.
- Câu trả lời có nguồn và có thể kiểm chứng.
- Tỷ lệ feedback sai ở mức chấp nhận được và giảm dần.
- Reviewer xử lý được feedback sai.
- Có wiki published từ các tài liệu hay được hỏi.
- Không phát sinh lỗi phân quyền nghiêm trọng.
- User đánh giá việc tìm thông tin nhanh hơn cách cũ.

## 3. Phạm Vi MVP

Trong phạm vi MVP:

- Auth, user, role, team.
- Folder tree và phân quyền theo team/user.
- Upload tài liệu PDF, DOCX, Markdown, TXT.
- Versioning tài liệu.
- Reviewer approve/reject tài liệu.
- Background processing để extract text, normalize, chunk, embed và index.
- AI Q&A có citation.
- Feedback Correct/Incorrect.
- Reviewer queue cho feedback sai.
- Wiki draft, publish/reject wiki.
- Dashboard KPI.
- Audit log.
- Local development và deploy IIS cơ bản.

Ngoài phạm vi MVP:

- Multi-tenant thương mại hoàn chỉnh.
- SSO/enterprise identity.
- OCR chất lượng production cho scan ảnh.
- Workflow approval nhiều cấp.
- Realtime collaboration.
- Fine-tuning model riêng.
- Billing, license, tenant admin portal.
- Data retention policy đầy đủ cho production lớn.

## 4. Người Dùng Chính

User:

- Upload tài liệu.
- Hỏi AI.
- Đọc citation.
- Gửi feedback.

Reviewer:

- Duyệt tài liệu.
- Xử lý feedback sai.
- Tạo và publish wiki.
- Theo dõi dashboard.

Admin:

- Quản lý user/team/folder.
- Phân quyền.
- Xem dashboard và audit log.
- Hỗ trợ vận hành.

## 5. Trạng Thái Triển Khai

Các phần cốt lõi đã có trong MVP:

- Backend API theo module chính.
- Frontend cho các luồng nghiệp vụ chính.
- SQLite cho dữ liệu nghiệp vụ.
- ChromaDB cho vector retrieval local.
- Workflow upload, approve, process và index tài liệu.
- AI Q&A với retrieval, citation và lưu lịch sử.
- Feedback loop cơ bản.
- Wiki draft/publish.
- Dashboard và audit log.
- Test backend/frontend và smoke script.

Các phần cần tiếp tục theo dõi:

- Chất lượng retrieval trên dữ liệu thật.
- Chất lượng chunking với tài liệu dài hoặc cấu trúc kém.
- Trải nghiệm reviewer khi queue nhiều.
- Độ rõ của citation trong các câu trả lời khó.
- Quy trình vận hành backup/restore khi deploy thật.
- Bảo mật dữ liệu khi mở rộng ngoài pilot nhỏ.

## 6. Tiêu Chí Hoàn Thành MVP

Project setup:

- Backend, frontend và local services chạy được theo tài liệu setup.
- `.env.example` đủ biến cấu hình cần thiết.
- Có seed data hoặc tài khoản demo rõ ràng.

Core backend:

- Auth, user, team, folder, permission hoạt động.
- Document upload/versioning hoạt động.
- Reviewer approve/reject hoạt động.
- Audit log ghi nhận hành động quan trọng.

RAG và AI:

- Tài liệu approved được xử lý và index.
- AI chỉ dùng nguồn user có quyền truy cập.
- Câu trả lời có citation.
- Khi không đủ thông tin, AI không bịa.

Wiki:

- Reviewer tạo wiki draft từ tài liệu approved.
- Reviewer publish hoặc reject wiki draft.
- Wiki published được index và dùng trong retrieval.

Feedback và dashboard:

- User gửi feedback Correct/Incorrect.
- Reviewer xem và xử lý feedback Incorrect.
- Dashboard hiển thị KPI chính.

Frontend:

- Các flow chính chạy được cho Admin, Reviewer, User.
- Empty/error states đủ rõ để demo/pilot.

Verification:

- Backend tests pass.
- Frontend build/tests pass.
- Smoke script pass hoặc lỗi đã được ghi nhận.

Deployment readiness:

- Có hướng dẫn deploy IIS.
- Có checklist bảo mật.
- Có hướng dẫn backup cơ bản.

## 7. Giới Hạn Hiện Tại

Product scope:

- MVP phù hợp pilot nhỏ, chưa phải hệ thống production lớn cho toàn công ty.
- Quy trình review còn đơn giản.
- Dashboard phục vụ đánh giá pilot, chưa phải analytics đầy đủ.

Document và wiki:

- File scan ảnh cần OCR ngoài hoặc nâng cấp thêm.
- Tài liệu quá dài, thiếu heading hoặc cấu trúc kém có thể làm retrieval kém chính xác.
- Wiki generation vẫn cần reviewer kiểm tra trước khi publish.

Search và RAG:

- Retrieval phụ thuộc chất lượng embedding, chunking và metadata.
- Câu hỏi quá rộng có thể cần user chọn scope cụ thể hơn.
- Citation phải được kiểm tra khi dùng cho quyết định quan trọng.

Operations và security:

- Pilot nên chạy với dữ liệu đã được phép chia sẻ.
- Cần quy trình backup/restore rõ hơn trước khi mở rộng.
- Cần kiểm tra phân quyền kỹ khi thêm team/folder mới.

AI provider:

- Chất lượng câu trả lời phụ thuộc provider/model được cấu hình.
- Cần kiểm soát prompt, JSON schema và fallback khi provider trả output lỗi.

## 8. Roadmap

Ưu tiên gần:

- Làm retrieval ổn định hơn trên dữ liệu thật.
- Cải thiện explain retrieval cho Reviewer/Admin.
- Tối ưu chunking và metadata cho tài liệu dài.
- Bổ sung evaluation before/after cho chất lượng AI.
- Làm rõ báo cáo pilot và top cited sources.

v1.1 candidates:

- OCR cho tài liệu scan.
- Rebuild knowledge index theo folder/document.
- UI tốt hơn cho document processing report.
- Reviewer tools để phân loại nguyên nhân feedback sai.
- Export báo cáo pilot.

v1.2 candidates:

- SSO hoặc tích hợp identity nội bộ.
- Workflow approval nhiều cấp.
- Better permission audit.
- Cấu hình retention và xóa dữ liệu.
- Tối ưu latency/cost cho AI provider.

v2 candidates:

- Multi-tenant.
- Admin portal đầy đủ.
- Observability production.
- Tích hợp nguồn tri thức ngoài file upload.
- Chính sách compliance và data governance đầy đủ hơn.

## 9. Thứ Tự Ưu Tiên Khi Cần Cắt Scope

Giữ lại:

- Auth và phân quyền.
- Upload tài liệu.
- Reviewer approve/reject.
- Document processing/index.
- AI Q&A có citation.
- Feedback Incorrect.

Có thể cắt hoặc làm sau:

- Dashboard nâng cao.
- Wiki generation nâng cao.
- Productization/multi-tenant.
- Advanced analytics.
- OCR production.
- Workflow approval nhiều cấp.

## 10. Tài Liệu Liên Quan

- [Mục lục tài liệu](MỤC_LỤC_TÀI_LIỆU.md)
- [Technical system overview](technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md)
- [API spec](../ĐẶC_TẢ_API.md)
- [Data model](../MÔ_HÌNH_DỮ_LIỆU.md)
- [Document upload to knowledge flow](technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md)
- [AI question to answer flow](technical/LUỒNG_HỎI_ĐÁP_AI.md)
- [Productization plan](technical/KẾ_HOẠCH_SẢN_PHẨM_HÓA.md)

