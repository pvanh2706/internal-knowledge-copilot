# Mục Lục Tài Liệu Dự Án

Tài liệu này là điểm bắt đầu để tìm nhanh tài liệu của dự án Internal Knowledge Copilot. Bộ tài liệu đã được gộp lại để giảm số file trùng nội dung.

## Cách Dùng Nhanh

| Nhu cầu | Nên đọc |
|---|---|
| Muốn hiểu tổng quan dự án | [README](../GIỚI_THIỆU.md), [Tổng quan dự án, trạng thái và roadmap](TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md), [Technical system overview](technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md) |
| Muốn chạy dự án local | [Local setup guide](technical/HƯỚNG_DẪN_CÀI_ĐẶT_LOCAL.md), [Development guide](../HƯỚNG_DẪN_PHÁT_TRIỂN.md), [Troubleshooting](technical/KHẮC_PHỤC_LỖI.md) |
| Muốn hiểu upload tài liệu thành tri thức | [Document upload to knowledge flow](technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md), [Simulated text to knowledge flow](technical/GIẢ_LẬP_TEXT_THÀNH_TRI_THỨC.md) |
| Muốn hiểu AI trả lời như thế nào | [AI question to answer flow](technical/LUỒNG_HỎI_ĐÁP_AI.md), [Technical system overview](technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md) |
| Muốn thấy rõ tác dụng khi có/không có wiki | [So sánh có và không có wiki](presentation/SO_SÁNH_CÓ_VÀ_KHÔNG_CÓ_WIKI.md) |
| Muốn có file mẫu để upload và demo wiki | [File upload demo quy trình thanh toán](demo-upload/QUY_TRINH_THANH_TOAN_NOI_BO_DEMO_WIKI.md), [Hướng dẫn demo upload wiki](presentation/HƯỚNG_DẪN_DEMO_UPLOAD_WIKI.md) |
| Muốn biết API và database | [API spec](../ĐẶC_TẢ_API.md), [Data model](../MÔ_HÌNH_DỮ_LIỆU.md) |
| Muốn tích hợp hệ thống bên thứ 3 | [Hướng dẫn tích hợp bên thứ 3](technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md), [Integration architecture notes](technical/GOI_Y_DIEU_CHINH_KIEN_TRUC_TICH_HOP.md), [Multi-tenant integration plan](technical/AI_IMPLEMENTATION_PLAN_MULTI_TENANT_INTEGRATION.md) |
| Muốn test hoặc demo | [Smart AI test guide](technical/HƯỚNG_DẪN_TEST_AI_THÔNG_MINH.md), [Hướng dẫn trình bày và demo](presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md) |
| Muốn chuẩn bị pilot cho người dùng | [Hướng dẫn pilot và sử dụng](pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md), [Support and issue templates](pilot/MẪU_HỖ_TRỢ_VÀ_BÁO_LỖI.md), [Pilot success report template](pilot/MẪU_BÁO_CÁO_KẾT_QUẢ_PILOT.md) |
| Muốn bàn giao cho AI hoặc người mới | [AI handoff](../BÀN_GIAO_AI.md), [Technical system overview](technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md), [Coding rules](../QUY_TẮC_CODE.md) |

## Tài Liệu Chính

| Tài liệu | Dùng để làm gì |
|---|---|
| [README](../GIỚI_THIỆU.md) | Điểm vào chung của repo, stack, cấu trúc, lệnh chạy và link tài liệu chính. |
| [Tổng quan dự án, trạng thái và roadmap](TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md) | Mục tiêu, phạm vi MVP, KPI, trạng thái, giới hạn, roadmap và tiêu chí hoàn thành. |
| [Architecture MVP](../KIẾN_TRÚC_MVP.md) | Kiến trúc MVP, module chính, dữ liệu, luồng xử lý, phân quyền và quyết định kiến trúc. |
| [AI handoff](../BÀN_GIAO_AI.md) | Tóm tắt dành cho AI agent hoặc người mới tiếp nhận task trong repo. |

## Kỹ Thuật Và Kiến Trúc

| Tài liệu | Dùng để làm gì |
|---|---|
| [Technical system overview for team and AI](technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md) | Tổng quan kỹ thuật đầy đủ: stack, thư mục code, domain model, luồng upload, AI Q&A, wiki và security. |
| [API spec](../ĐẶC_TẢ_API.md) | Đặc tả endpoint MVP cho auth, users, teams, folders, documents, AI Q&A, feedback, wiki, dashboard và audit logs. |
| [Hướng dẫn tích hợp bên thứ 3](technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md) | Hướng dẫn partner integration: auth, headers, payload mẫu, curl/Postman, sync tài liệu, sync quyền, hỏi AI theo quyền, lỗi thường gặp và checklist go-live. |
| [Data model](../MÔ_HÌNH_DỮ_LIỆU.md) | Mô hình dữ liệu MVP: enum, bảng SQLite, quan hệ nghiệp vụ và collection vector. |
| [UI flow](../LUỒNG_GIAO_DIỆN.md) | Luồng màn hình frontend theo vai trò Admin, User, Reviewer và các trạng thái UI chính. |
| [Coding rules](../QUY_TẮC_CODE.md) | Quy tắc code backend, frontend, database, RAG, prompt, logging và testing. |
| [Development guide](../HƯỚNG_DẪN_PHÁT_TRIỂN.md) | Hướng dẫn phát triển, cấu trúc source, local services, config, seed data và vòng lặp làm việc. |
| [Testing guide](../HƯỚNG_DẪN_KIỂM_THỬ.md) | Triết lý test, backend/frontend tests, smoke test flow, build gates và kiểm tra bảo mật thủ công. |
| [Security checklist](../CHECKLIST_BẢO_MẬT.md) | Checklist bảo mật MVP cho auth, authorization, file handling, RAG, audit, backup và pilot. |
| [Deployment IIS](../TRIỂN_KHAI_IIS.md) | Hướng dẫn build, cấu hình IIS, migration database, ChromaDB, backup và smoke verification sau deploy. |

## Luồng AI, RAG Và Xử Lý Tài Liệu

| Tài liệu | Dùng để làm gì |
|---|---|
| [Document upload to knowledge flow](technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md) | Luồng chi tiết từ upload tài liệu, approve, background job, extract text, normalize, chunk, embedding đến index. |
| [AI question to answer flow](technical/LUỒNG_HỎI_ĐÁP_AI.md) | Luồng chi tiết từ câu hỏi người dùng đến retrieval, rerank, context packing, gọi LLM, citation và lưu lịch sử. |
| [Simulated text to knowledge flow](technical/GIẢ_LẬP_TEXT_THÀNH_TRI_THỨC.md) | Ví dụ giả lập một đoạn text đi qua code cho đến khi thành tri thức có thể truy vấn. |
| [Smart AI upgrade plan](technical/KẾ_HOẠCH_NÂNG_CẤP_AI_THÔNG_MINH.md) | Kế hoạch nâng cấp AI: provider, smart ingestion, permission-aware retrieval, hybrid retrieval, evaluation và knowledge rebuild. |
| [Smart AI test guide](technical/HƯỚNG_DẪN_TEST_AI_THÔNG_MINH.md) | Bộ kịch bản test để chứng minh AI thông minh hơn, có citation, phân quyền, retrieval explain, feedback loop và dashboard. |
| [AI harness assessment](technical/ĐÁNH_GIÁ_AI_HARNESS.md) | Đánh giá dự án theo góc nhìn AI Harness, điểm mạnh, rủi ro và roadmap trưởng thành. |
| [Productization plan](technical/KẾ_HOẠCH_SẢN_PHẨM_HÓA.md) | Kế hoạch đưa hệ thống từ demo/MVP sang hướng sản phẩm ổn định hơn. |

## Cài Đặt, Vận Hành Và Khắc Phục Lỗi

| Tài liệu | Dùng để làm gì |
|---|---|
| [Local setup guide](technical/HƯỚNG_DẪN_CÀI_ĐẶT_LOCAL.md) | Hướng dẫn chuẩn bị môi trường local để chạy backend, frontend và các dịch vụ liên quan. |
| [Troubleshooting](technical/KHẮC_PHỤC_LỖI.md) | Cách xử lý lỗi thường gặp: thiếu dotnet, npm install fail, ChromaDB, port, login, upload, AI không tìm thấy tài liệu. |
| [Deployment IIS](../TRIỂN_KHAI_IIS.md) | Tài liệu triển khai, backup và kiểm tra sau deploy trên IIS. |

## Pilot, Demo Và Trình Bày

| Tài liệu | Dùng để làm gì |
|---|---|
| [Hướng dẫn pilot và sử dụng](pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md) | Tài liệu chính cho pilot: vai trò, user/reviewer/admin, dữ liệu, bảo mật, vận hành, KPI và FAQ. |
| [Support and issue templates](pilot/MẪU_HỖ_TRỢ_VÀ_BÁO_LỖI.md) | Mẫu báo lỗi, yêu cầu cấp quyền, báo AI trả lời sai, đề xuất thêm tài liệu hoặc tạo wiki. |
| [Pilot success report template](pilot/MẪU_BÁO_CÁO_KẾT_QUẢ_PILOT.md) | Mẫu báo cáo kết quả pilot, KPI, feedback, vấn đề phát sinh và đề xuất quyết định. |
| [Hướng dẫn trình bày và demo](presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md) | Thông điệp trình bày, outline slide, kịch bản demo, checklist, pilot plan và câu hỏi có thể gặp. |
| [So sánh có và không có wiki](presentation/SO_SÁNH_CÓ_VÀ_KHÔNG_CÓ_WIKI.md) | Giải thích tác dụng của wiki, bảng so sánh trước/sau, cách đo hiệu quả và kịch bản demo. |
| [Hướng dẫn demo upload wiki](presentation/HƯỚNG_DẪN_DEMO_UPLOAD_WIKI.md) | Cách dùng file mẫu để hỏi trước/sau khi publish wiki và ghi nhận khác biệt citation, confidence, source type. |

## Gợi Ý Đọc Theo Vai Trò

| Vai trò | Tài liệu nên đọc trước |
|---|---|
| Developer mới | [README](../GIỚI_THIỆU.md), [Development guide](../HƯỚNG_DẪN_PHÁT_TRIỂN.md), [Technical system overview](technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md), [Coding rules](../QUY_TẮC_CODE.md) |
| Backend developer | [API spec](../ĐẶC_TẢ_API.md), [Data model](../MÔ_HÌNH_DỮ_LIỆU.md), [Document upload to knowledge flow](technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md), [AI question to answer flow](technical/LUỒNG_HỎI_ĐÁP_AI.md) |
| Frontend developer | [UI flow](../LUỒNG_GIAO_DIỆN.md), [API spec](../ĐẶC_TẢ_API.md), [Hướng dẫn pilot và sử dụng](pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md) |
| AI/RAG developer | [Smart AI upgrade plan](technical/KẾ_HOẠCH_NÂNG_CẤP_AI_THÔNG_MINH.md), [AI question to answer flow](technical/LUỒNG_HỎI_ĐÁP_AI.md), [Smart AI test guide](technical/HƯỚNG_DẪN_TEST_AI_THÔNG_MINH.md) |
| Reviewer/Admin | [Hướng dẫn pilot và sử dụng](pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md), [Security checklist](../CHECKLIST_BẢO_MẬT.md) |
| Người chuẩn bị demo | [Hướng dẫn trình bày và demo](presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md), [Hướng dẫn pilot và sử dụng](pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md) |

## Quy Ước Cập Nhật

- Tài liệu chính nên được ưu tiên cập nhật thay vì tạo file mới.
- Nếu nội dung mới thuộc pilot, cập nhật `pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md`.
- Nếu nội dung mới thuộc demo/trình bày, cập nhật `presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md`.
- Nếu nội dung mới thuộc scope, trạng thái, roadmap hoặc giới hạn, cập nhật `TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md`.
- Nếu thêm tài liệu mới thật sự cần thiết, cập nhật mục lục này trong cùng thay đổi.

Lần cập nhật gần nhất: 2026-06-01.
