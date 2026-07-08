# Development Guide

This document gives an AI coding agent enough operational context to scaffold, run, and continue the project without asking for routine setup decisions.

## Source Of Truth

Use these files as the product and engineering source of truth:

- `docs/technical/HƯỚNG_DẪN_CÀI_ĐẶT_LOCAL.md`: fresh-machine clone, install, run, and smoke-test guide
- `docs/technical/KHẮC_PHỤC_LỖI.md`: local setup and runtime troubleshooting
- `BÀN_GIAO_AI.md`: fastest current-state briefing for future AI coding sessions
- `docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md`: business context, confirmed MVP scope, status, limitations, done criteria, and roadmap
- `KIẾN_TRÚC_MVP.md`: stack, modules, deployment direction
- `ĐẶC_TẢ_API.md`: API surface
- `MÔ_HÌNH_DỮ_LIỆU.md`: database model and vector payloads
- `LUỒNG_GIAO_DIỆN.md`: pages and role-based flows
- `docs/technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md`: document processing and knowledge indexing behavior
- `docs/technical/LUỒNG_HỎI_ĐÁP_AI.md`: retrieval, prompt, citation, and answer behavior
- `QUY_TẮC_CODE.md`: implementation rules and definition of done for tasks

If documents conflict, prefer the more specific implementation document. For example, API behavior in `ĐẶC_TẢ_API.md` takes precedence over a higher-level mention in `docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md`.

## Implementation Assumptions

- Backend should be ASP.NET Core.
- Frontend should be Vue.
- SQLite is the metadata database for MVP.
- ChromaDB is the implemented development/test vector database.
- Qdrant remains a future replacement option and should stay behind the vector-store adapter boundary.
- File uploads must be stored outside public web root.
- Authentication should use local username/password with JWT for MVP.
- Roles are `Admin`, `Reviewer`, and `User`.
- UI text should be Vietnamese.
- Code identifiers should be English.
- Code comments, when needed, should be Vietnamese with accents.

## Suggested Backend Structure

```text
src/backend/
  InternalKnowledgeCopilot.sln
  InternalKnowledgeCopilot.Api/
    Controllers/
    Modules/
      Auth/
      Users/
      Folders/
      Documents/
      Wiki/
      Ai/
      Dashboard/
      Audit/
    Infrastructure/
      Database/
      FileStorage/
      VectorStore/
      AiProvider/
      BackgroundJobs/
    Common/
  InternalKnowledgeCopilot.Tests/
```

Recommended backend choices:

- ASP.NET Core Web API
- Entity Framework Core with SQLite
- ASP.NET Core authentication/authorization
- Hosted service for background processing jobs
- Serilog or built-in logging to file
- xUnit or NUnit for backend tests

## Frontend Structure (implemented)

```text
src/frontend/
  components.json          # shadcn-vue config — add component đích danh, không re-init
  src/
    api/                   # API client theo module — KHÔNG đổi chữ ký khi làm UI
    assets/main.css        # Tailwind v4 + design tokens (cấu hình bằng CSS, KHÔNG có tailwind.config.js)
    components/
      ui/                  # shadcn-vue generate — sửa tay phải đánh dấu "// [đã sửa tay]"
      app/                 # component nghiệp vụ: DataTable, StatusBadge (+ status.ts), PageHeader,
                           # FilterBar, ConfirmDialog, EmptyState, ErrorState, CitationCard,
                           # ScopeSelector, AnswerPanel, FolderSelect, FileUpload, JsonViewer...
    composables/           # useTheme, useToast, useConfirm, useFolderTree, useAiSettingsForm
    layouts/               # AppLayout (sidebar + topbar) / AuthLayout — chọn theo route meta.layout
    lib/                   # cn(), formatDateTime
    pages/                 # auth/ documents/ ai/ wiki/ review/ admin/ workflow/
    router/                # route lazy-load, meta.layout + guard role
    stores/
    test/setup.ts          # stub ResizeObserver/matchMedia/scrollIntoView cho jsdom
```

Frontend stack (đã triển khai từ redesign UI 2026-07):

- Vue 3 + Vite, Pinia, Vue Router (layout chọn qua `meta.layout`)
- Tailwind CSS v4 + shadcn-vue (Reka UI), icon `@lucide/vue` (chỉ import đích danh)
- Font Inter Variable self-host (`@fontsource-variable/inter`) — không gọi CDN
- Dark mode: `useTheme()` 3 chế độ, key localStorage `theme`, inline script chống FOUC trong `index.html`
- Toast: vue-sonner qua `useToast()`; confirm hành động phá hủy qua `useConfirm()` (bắt buộc)
- Bảng dữ liệu: `DataTable` (@tanstack/vue-table, phân trang client-side)
- Vitest cho unit test; test đặt cạnh file nguồn

Quy ước khi sửa UI: cấm hardcode hex (chỉ dùng token qua utility như `bg-background`, `text-muted-foreground`), cấm tạo `tailwind.config.js`/`@tailwind base` (cú pháp v3), UI text tiếng Việt có dấu, badge trạng thái map qua `components/app/status.ts`. Chi tiết: [docs/KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md](docs/KẾ_HOẠCH_THIẾT_KẾ_LẠI_GIAO_DIỆN.md) mục 3 và 5.4.

## Local Services

ChromaDB should be runnable locally without Docker.

Expected command:

```powershell
chroma run --host localhost --port 8000 --path ./.chroma
```

SQLite should use a local file path configured through environment or appsettings.

## Configuration

Use `.env.example` as the list of required configuration values.

Do not commit real secrets. Real API keys, JWT signing keys, and production paths should stay in environment variables or secret storage.

## Seed Data

The app should provide development seed data so local smoke tests can run consistently:

```text
Admin:
  email: admin@example.local
  password: ChangeMe123!

Reviewer:
  email: reviewer@example.local
  password: ChangeMe123!

User:
  email: user@example.local
  password: ChangeMe123!

Teams:
  Ky thuat
  Ho tro khach hang
```

If the UI displays team names, use Vietnamese text with accents in the UI layer.

## Development Loop For AI Agents

For new feature work or bug fixes:

1. Read `BÀN_GIAO_AI.md`, `docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md`, and the relevant spec files.
2. Add the smallest slice that satisfies the requested change.
3. Add or update tests for the risky logic in that slice.
4. Run the relevant test/build commands.
5. Fix failures before starting unrelated work.
6. Keep README, `.env.example`, and this file current when commands or setup change.

Do not broaden scope beyond the requested change unless required to keep an accepted MVP flow working.

## Local Commands

Backend:

```powershell
dotnet restore src/backend/InternalKnowledgeCopilot.sln
dotnet build src/backend/InternalKnowledgeCopilot.sln
dotnet test src/backend/InternalKnowledgeCopilot.sln
dotnet run --project src/backend/InternalKnowledgeCopilot.Api
```

Frontend:

```powershell
cd src/frontend
npm install
npm run dev
npm run lint
npm run build
npm test
```

Yêu cầu Node >= 18 (khuyến nghị Node 22). 3 gate `lint`/`build`/`test` phải pass trước khi commit thay đổi frontend.

Infrastructure:

```powershell
chroma run --host localhost --port 8000 --path ./.chroma
```

## Current Implementation Notes

The repository now contains the implemented MVP, not documentation only.

Key implemented paths:

- Backend solution: `src/backend/InternalKnowledgeCopilot.sln`
- Backend API: `src/backend/InternalKnowledgeCopilot.Api`
- Backend tests: `src/backend/InternalKnowledgeCopilot.Tests`
- Frontend app: `src/frontend`
- Vector-store adapter boundary: `src/backend/InternalKnowledgeCopilot.Api/Infrastructure/VectorStore`
- Database migrations: `src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Database/Migrations`
- Smoke scripts: `scripts/smoke-mvp.ps1` and milestone-specific smoke scripts

Generated local runtime folders such as `.chroma`, `.run`, `data`, `logs`, and `storage` are not source-of-truth documentation.

