# Mục Lục Tài Liệu Dự Án

Ngày cập nhật: 2026-06-07

Đây là điểm bắt đầu để đọc tài liệu Internal Knowledge Copilot. Mục lục ưu tiên tài liệu hiện hành; tài liệu trùng vai trò hoặc chỉ còn giá trị lịch sử đã chuyển vào `docs/archive/`.

## Cách Dùng Nhanh

| Nhu cầu | Nên đọc |
| --- | --- |
| Hiểu tổng quan dự án | [README](../GIỚI_THIỆU.md), [Tổng quan trạng thái roadmap](TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md), [Technical system overview](technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md) |
| Chạy local | [Local setup guide](technical/HƯỚNG_DẪN_CÀI_ĐẶT_LOCAL.md), [Development guide](../HƯỚNG_DẪN_PHÁT_TRIỂN.md), [Troubleshooting](technical/KHẮC_PHỤC_LỖI.md) |
| Hiểu kiến trúc và dữ liệu | [Architecture MVP](../KIẾN_TRÚC_MVP.md), [Data model](../MÔ_HÌNH_DỮ_LIỆU.md), [Danh sách bảng hiện tại](technical/DANH_SACH_BANG_DU_LIEU_HE_THONG.md) |
| Hiểu upload thành tri thức | [Document upload to knowledge flow](technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md) |
| Hiểu AI trả lời | [AI question to answer flow](technical/LUỒNG_HỎI_ĐÁP_AI.md), [AI harness assessment](technical/ĐÁNH_GIÁ_AI_HARNESS.md) |
| Tích hợp hệ thống bên thứ 3 | [Hướng dẫn tích hợp và triển khai kỹ thuật cho bên thứ 3](technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md), [Postman collection](postman/InternalKnowledgeCopilot_ThirdPartyIntegration.postman_collection.json) |
| Test hoặc demo | [Testing guide](../HƯỚNG_DẪN_KIỂM_THỬ.md), [Smart AI test guide](technical/HƯỚNG_DẪN_TEST_AI_THÔNG_MINH.md), [Hướng dẫn trình bày và demo](presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md) |
| Đào tạo team triển khai/tư vấn khách hàng | [Hướng dẫn đào tạo team triển khai](training/HƯỚNG_DẪN_ĐÀO_TẠO_TEAM_TRIỂN_KHAI.md), [Hướng dẫn pilot và sử dụng](pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md) |
| Chuẩn bị pilot | [Hướng dẫn pilot và sử dụng](pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md), [Support templates](pilot/MẪU_HỖ_TRỢ_VÀ_BÁO_LỖI.md), [Pilot report template](pilot/MẪU_BÁO_CÁO_KẾT_QUẢ_PILOT.md) |
| Bàn giao cho AI/người mới | [AI handoff](../BÀN_GIAO_AI.md), [Technical system overview](technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md), [Coding rules](../QUY_TẮC_CODE.md) |

## Tài Liệu Hiện Hành

| Loại | Tài liệu | Vai trò |
| --- | --- | --- |
| current | [README](../GIỚI_THIỆU.md) | Điểm vào repo, stack, lệnh chạy và link chính. |
| current | [Tổng quan trạng thái roadmap](TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md) | Mục tiêu, scope, KPI, trạng thái, giới hạn và roadmap. |
| current | [Technical system overview](technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md) | Tài liệu kỹ thuật tập trung cho team và AI coding agent. |
| current | [Architecture MVP](../KIẾN_TRÚC_MVP.md) | Kiến trúc gọn theo implementation hiện tại. |
| current | [Data model](../MÔ_HÌNH_DỮ_LIỆU.md) | Tóm tắt model và nguyên tắc source of truth. |
| current | [Danh sách bảng hiện tại](technical/DANH_SACH_BANG_DU_LIEU_HE_THONG.md) | Bảng SQLite hiện có, đối chiếu theo code. |
| current | [API spec](../ĐẶC_TẢ_API.md) | Endpoint chính của MVP. |
| current | [UI flow](../LUỒNG_GIAO_DIỆN.md) | Luồng màn hình theo vai trò. |
| current | [Coding rules](../QUY_TẮC_CODE.md) | Quy tắc code, RAG, logging, testing và definition of done. |

## Luồng Kỹ Thuật

| Loại | Tài liệu | Vai trò |
| --- | --- | --- |
| deep-dive | [Document upload to knowledge flow](technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md) | Luồng upload, approve, processing, chunk, embedding, index và wiki publish. |
| deep-dive | [AI question to answer flow](technical/LUỒNG_HỎI_ĐÁP_AI.md) | Retrieval, permission recheck, rerank, context packing, LLM call và citation. |
| deep-dive | [Smart AI upgrade plan](technical/KẾ_HOẠCH_NÂNG_CẤP_AI_THÔNG_MINH.md) | Kế hoạch nâng cấp AI, ingestion, retrieval, evaluation và rebuild. |
| deep-dive | [AI harness assessment](technical/ĐÁNH_GIÁ_AI_HARNESS.md) | Đánh giá mức trưởng thành RAG/harness và rủi ro còn lại. |
| deep-dive | [Productization plan](technical/KẾ_HOẠCH_SẢN_PHẨM_HÓA.md) | Hướng đưa MVP thành sản phẩm ổn định hơn. |
| current | [Third-party integration and technical deployment guide](technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md) | Tư vấn chức năng, topology, cấu hình triển khai, contract, header, payload, sync, workflow và checklist tích hợp. |
| current | [Postman collection tích hợp bên thứ 3](postman/InternalKnowledgeCopilot_ThirdPartyIntegration.postman_collection.json) | Collection v2.1 để bên thứ ba import vào Postman và test JWT, integration key, sync object/permission, AI Q&A và workflow. |
| current | [Enterprise operations notes](technical/ENTERPRISE_OPERATIONS_NOTES.md) | Secret handling, DB migration, backup, restore, retention và future research. |

## Cài Đặt, Vận Hành Và Test

| Loại | Tài liệu | Vai trò |
| --- | --- | --- |
| current | [Local setup guide](technical/HƯỚNG_DẪN_CÀI_ĐẶT_LOCAL.md) | Chuẩn bị môi trường local. |
| current | [Troubleshooting](technical/KHẮC_PHỤC_LỖI.md) | Lỗi thường gặp khi setup, chạy, upload và AI retrieval. |
| current | [Testing guide](../HƯỚNG_DẪN_KIỂM_THỬ.md) | Test strategy và smoke command chuẩn. |
| current | [Deployment IIS](../TRIỂN_KHAI_IIS.md) | Build, IIS, ChromaDB, backup và smoke verification sau deploy. |
| deep-dive | [Smart AI test guide](technical/HƯỚNG_DẪN_TEST_AI_THÔNG_MINH.md) | Bộ kịch bản kiểm tra AI/RAG chuyên sâu. |

## Pilot, Demo Và Trình Bày

| Loại | Tài liệu | Vai trò |
| --- | --- | --- |
| current | [Hướng dẫn đào tạo team triển khai](training/HƯỚNG_DẪN_ĐÀO_TẠO_TEAM_TRIỂN_KHAI.md) | Tài liệu đào tạo team triển khai/tư vấn: chức năng, vai trò, demo, onboarding, FAQ và checklist pilot. |
| current | [Hướng dẫn pilot và sử dụng](pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md) | Tài liệu chính cho user, reviewer, admin và vận hành pilot. |
| template | [Support templates](pilot/MẪU_HỖ_TRỢ_VÀ_BÁO_LỖI.md) | Mẫu báo lỗi và yêu cầu hỗ trợ. |
| template | [Pilot success report template](pilot/MẪU_BÁO_CÁO_KẾT_QUẢ_PILOT.md) | Mẫu báo cáo KPI và kết quả pilot. |
| current | [Hướng dẫn trình bày và demo](presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md) | Thông điệp, outline slide, demo script và checklist. |
| current | [So sánh có và không có wiki](presentation/SO_SÁNH_CÓ_VÀ_KHÔNG_CÓ_WIKI.md) | Giải thích tác dụng wiki và kịch bản so sánh. |
| current | [Hướng dẫn demo upload wiki](presentation/HƯỚNG_DẪN_DEMO_UPLOAD_WIKI.md) | Cách dùng file mẫu để demo trước/sau publish wiki. |
| demo-data | [File upload demo quy trình thanh toán](demo-upload/QUY_TRINH_THANH_TOAN_NOI_BO_DEMO_WIKI.md) | Bộ dữ liệu demo wiki. |

## Archive

Các file trong archive không phải source of truth hiện hành:

| Loại | Tài liệu | Lý do archive |
| --- | --- | --- |
| archive | [Vietnamese user manual](archive/pilot/VIETNAMESE_USER_MANUAL.md) | Trùng với tài liệu pilot chính. |
| archive | [Simulated text to knowledge flow](archive/technical/GIẢ_LẬP_TEXT_THÀNH_TRI_THỨC.md) | Trùng với upload flow, chỉ còn giá trị ví dụ lịch sử. |
| archive | [Integration clarification summary](archive/technical/TONG_HOP_LAM_RO_DINH_HUONG_TICH_HOP.md) | Đã được thay bằng integration guide và implementation history. |
| archive | [Integration architecture notes](archive/technical/GOI_Y_DIEU_CHINH_KIEN_TRUC_TICH_HOP.md) | Đã được thay bằng integration guide. |
| archive | [Multi-tenant integration implementation plan](archive/technical/AI_IMPLEMENTATION_PLAN_MULTI_TENANT_INTEGRATION.md) | Nhật ký/plan lịch sử, không phải hướng dẫn đọc chính. |

## Quy Ước Cập Nhật

- Ưu tiên cập nhật tài liệu `current` thay vì tạo file mới.
- Nếu nội dung thuộc pilot/user guide, cập nhật `pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md`.
- Nếu nội dung thuộc demo/trình bày, cập nhật `presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md`.
- Nếu nội dung thuộc scope, trạng thái, roadmap hoặc giới hạn, cập nhật `TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md`.
- Nếu nội dung thuộc schema hiện tại, cập nhật `technical/DANH_SACH_BANG_DU_LIEU_HE_THONG.md` và `../MÔ_HÌNH_DỮ_LIỆU.md`.
- Nếu thêm hoặc archive tài liệu, cập nhật mục lục này trong cùng thay đổi.
