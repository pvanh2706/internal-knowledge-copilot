# Internal Knowledge Copilot

Internal Knowledge Copilot là MVP quản lý tri thức nội bộ với upload tài liệu, reviewer approval, AI Q&A dùng RAG có citation, feedback, wiki draft generation, reviewer publishing, dashboard KPI và audit log.

Điểm bắt đầu khi đọc tài liệu:

- [Mục lục tài liệu](docs/MỤC_LỤC_TÀI_LIỆU.md)
- [Tổng quan dự án, trạng thái và roadmap](docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md)
- [Technical system overview](docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md)
- [AI handoff](BÀN_GIAO_AI.md)

## MVP Stack

- Backend: ASP.NET Core API
- Frontend: Vue 3 + TypeScript + Vite
- Metadata database: SQLite
- Vector database hiện tại: ChromaDB
- Vector database mục tiêu ban đầu: Qdrant, vẫn giữ sau adapter boundary để có thể thay thế sau
- File storage: local filesystem ngoài public web root
- Deployment target: Windows Server / IIS

## Project Structure

```text
src/
  backend/
    InternalKnowledgeCopilot.Api/
    InternalKnowledgeCopilot.Tests/
  frontend/
docs/
scripts/
.env.example
```

## Tài Liệu Chính

Product và điều phối:

- [docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md](docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md)
- [KIẾN_TRÚC_MVP.md](KIẾN_TRÚC_MVP.md)
- [ĐẶC_TẢ_API.md](ĐẶC_TẢ_API.md)
- [MÔ_HÌNH_DỮ_LIỆU.md](MÔ_HÌNH_DỮ_LIỆU.md)
- [LUỒNG_GIAO_DIỆN.md](LUỒNG_GIAO_DIỆN.md)

Kỹ thuật:

- [docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md](docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md)
- [docs/technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md](docs/technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md)
- [docs/postman/InternalKnowledgeCopilot_ThirdPartyIntegration.postman_collection.json](docs/postman/InternalKnowledgeCopilot_ThirdPartyIntegration.postman_collection.json)
- [docs/technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md](docs/technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md)
- [docs/technical/LUỒNG_HỎI_ĐÁP_AI.md](docs/technical/LUỒNG_HỎI_ĐÁP_AI.md)
- [docs/technical/HƯỚNG_DẪN_CÀI_ĐẶT_LOCAL.md](docs/technical/HƯỚNG_DẪN_CÀI_ĐẶT_LOCAL.md)
- [docs/technical/KHẮC_PHỤC_LỖI.md](docs/technical/KHẮC_PHỤC_LỖI.md)

Vận hành, demo và pilot:

- [docs/training/HƯỚNG_DẪN_ĐÀO_TẠO_TEAM_TRIỂN_KHAI.md](docs/training/HƯỚNG_DẪN_ĐÀO_TẠO_TEAM_TRIỂN_KHAI.md)
- [docs/pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md](docs/pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md)
- [docs/presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md](docs/presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md)
- [docs/pilot/MẪU_HỖ_TRỢ_VÀ_BÁO_LỖI.md](docs/pilot/MẪU_HỖ_TRỢ_VÀ_BÁO_LỖI.md)
- [docs/pilot/MẪU_BÁO_CÁO_KẾT_QUẢ_PILOT.md](docs/pilot/MẪU_BÁO_CÁO_KẾT_QUẢ_PILOT.md)

## Local Development

Với máy mới, bắt đầu từ [docs/technical/HƯỚNG_DẪN_CÀI_ĐẶT_LOCAL.md](docs/technical/HƯỚNG_DẪN_CÀI_ĐẶT_LOCAL.md). Xem thêm [HƯỚNG_DẪN_PHÁT_TRIỂN.md](HƯỚNG_DẪN_PHÁT_TRIỂN.md) nếu cần chi tiết về cấu trúc source và vòng lặp phát triển.

Lệnh chạy local:

```powershell
chroma run --host localhost --port 8000 --path ./.chroma
dotnet restore src/backend/InternalKnowledgeCopilot.sln
dotnet run --project src/backend/InternalKnowledgeCopilot.Api
cd src/frontend
npm install
npm run dev
```

## Testing

Xem [HƯỚNG_DẪN_KIỂM_THỬ.md](HƯỚNG_DẪN_KIỂM_THỬ.md) cho test strategy.

Lệnh kiểm tra:

```powershell
dotnet test src/backend/InternalKnowledgeCopilot.sln
cd src/frontend
npm test
npm run build
powershell -ExecutionPolicy Bypass -File ../../scripts/smoke-mvp.ps1
```

## AI Working Instructions

AI coding agent nên đọc theo thứ tự:

1. [BÀN_GIAO_AI.md](BÀN_GIAO_AI.md)
2. [docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md](docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md)
3. [docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md](docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md)
4. File spec liên quan: [ĐẶC_TẢ_API.md](ĐẶC_TẢ_API.md), [MÔ_HÌNH_DỮ_LIỆU.md](MÔ_HÌNH_DỮ_LIỆU.md), [LUỒNG_GIAO_DIỆN.md](LUỒNG_GIAO_DIỆN.md)
5. Flow kỹ thuật liên quan trong `docs/technical`
6. [QUY_TẮC_CODE.md](QUY_TẮC_CODE.md)
7. Source module và tests liên quan

Sau mỗi thay đổi, chạy build/test phù hợp và tiếp tục sửa lỗi cho đến khi task hoàn tất hoặc gặp blocker thật.

## Current Vector DB Decision

Dùng ChromaDB cho development và test vì có sẵn trong môi trường này và không yêu cầu Docker. Vector operations nằm sau application interface để sau này có thể thêm Qdrant mà không đổi business flow của document processing hoặc AI Q&A.

## Deployment And Backup

Use [TRIỂN_KHAI_IIS.md](TRIỂN_KHAI_IIS.md) for Windows Server / IIS deployment notes and backup guidance. Use [CHECKLIST_BẢO_MẬT.md](CHECKLIST_BẢO_MẬT.md) as the MVP pilot readiness checklist.

