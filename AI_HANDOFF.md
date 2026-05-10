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

1. `README.md`
2. `PHASE_STATUS.md`
3. `KNOWN_LIMITATIONS.md`
4. `ROADMAP.md`
5. The specific feature spec, usually one of `API_SPEC.md`, `DATA_MODEL.md`, `UI_FLOW.md`, or `RAG_AND_WIKI_FLOW.md`
6. `CODING_RULES.md`
7. The relevant source modules and tests

Use `IMPLEMENTATION_PLAN.md` mainly as milestone history. The MVP milestones are already implemented.

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

Before adding large features, check `KNOWN_LIMITATIONS.md` and `ROADMAP.md`. Some missing features are intentional MVP deferrals, not bugs.

