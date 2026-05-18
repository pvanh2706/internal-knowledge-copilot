# AI Handoff

Last updated: 2026-05-10

This file is the first document an AI coding agent should read when returning to this project after a long break.

## Project Summary

Internal Knowledge Copilot is an internal knowledge management MVP. It supports document upload and review, document versioning, RAG-based AI Q&A with citations, feedback review, wiki draft generation, wiki publishing, dashboard KPIs, and audit logs.

## Current Implementation

- Backend: ASP.NET Core API
- Frontend: Vue 3 + TypeScript + Vite
- Metadata database: SQLite through Entity Framework Core migrations
- Current vector database: ChromaDB
- Future vector database option: Qdrant, kept behind a vector-store adapter boundary
- File storage: local filesystem outside public web root
- Deployment target: Windows Server / IIS

## Important Paths

- Backend solution: `src/backend/InternalKnowledgeCopilot.sln`
- Backend API: `src/backend/InternalKnowledgeCopilot.Api`
- Backend tests: `src/backend/InternalKnowledgeCopilot.Tests`
- Frontend app: `src/frontend`
- Backend modules: `src/backend/InternalKnowledgeCopilot.Api/Modules`
- Database context and migrations: `src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Database`
- Vector-store adapter: `src/backend/InternalKnowledgeCopilot.Api/Infrastructure/VectorStore`
- Document processing: `src/backend/InternalKnowledgeCopilot.Api/Infrastructure/DocumentProcessing`
- AI provider abstractions/services: `src/backend/InternalKnowledgeCopilot.Api/Infrastructure/AiProvider`
- Smoke scripts: `scripts`

## Read Next

For most future feature work, read in this order:

1. `GIỚI_THIỆU.md`
2. `docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md`
3. `docs/technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md`
4. The specific feature spec, usually one of `ĐẶC_TẢ_API.md`, `MÔ_HÌNH_DỮ_LIỆU.md`, `LUỒNG_GIAO_DIỆN.md`, `docs/technical/LUỒNG_UPLOAD_TÀI_LIỆU_THÀNH_TRI_THỨC.md`, or `docs/technical/LUỒNG_HỎI_ĐÁP_AI.md`
5. `QUY_TẮC_CODE.md`
6. The relevant source modules and tests

## Verification Commands

Backend:

```powershell
dotnet restore src/backend/InternalKnowledgeCopilot.sln
dotnet build src/backend/InternalKnowledgeCopilot.sln
dotnet test src/backend/InternalKnowledgeCopilot.sln
```

Frontend:

```powershell
cd src/frontend
npm install
npm run build
npm test
```

Full local smoke:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-mvp.ps1
```

The smoke script starts temporary infrastructure where possible and verifies the main MVP flow.

## Design Rules To Preserve

- SQLite is the source of truth for identity, permissions, documents, wiki, feedback, dashboard, and audit data.
- ChromaDB/Qdrant is not the source of truth for permissions.
- Backend must recheck retrieved vector chunks against SQLite before using them in AI answers.
- Only approved/current document versions and published wiki pages may be used for AI Q&A.
- Published wiki should be preferred over raw document chunks when relevant.
- Uploaded files must stay outside public web root and downloads must go through authorized API endpoints.
- UI text is Vietnamese; code identifiers are English.

## Current Scope Boundary

Before adding large features, check `docs/TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md`. Some missing features are intentional MVP deferrals, not bugs.


