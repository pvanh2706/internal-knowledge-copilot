# Development Guide

This document gives an AI coding agent enough operational context to scaffold, run, and continue the project without asking for routine setup decisions.

## Source Of Truth

Use these files as the product and engineering source of truth:

- `docs/technical/LOCAL_SETUP_GUIDE.md`: fresh-machine clone, install, run, and smoke-test guide
- `docs/technical/TROUBLESHOOTING.md`: local setup and runtime troubleshooting
- `AI_HANDOFF.md`: fastest current-state briefing for future AI coding sessions
- `REQUIREMENTS_DISCOVERY.md`: business context and confirmed MVP scope
- `ARCHITECTURE_MVP.md`: stack, modules, deployment direction
- `IMPLEMENTATION_PLAN.md`: milestone order and acceptance criteria
- `API_SPEC.md`: API surface
- `DATA_MODEL.md`: database model and vector payloads
- `UI_FLOW.md`: pages and role-based flows
- `RAG_AND_WIKI_FLOW.md`: indexing, retrieval, prompt behavior
- `CODING_RULES.md`: implementation rules and definition of done for tasks
- `PHASE_STATUS.md`: completed milestone history
- `KNOWN_LIMITATIONS.md`: intentionally deferred or incomplete areas
- `ROADMAP.md`: recommended post-MVP improvements

If documents conflict, prefer the more specific implementation document. For example, API behavior in `API_SPEC.md` takes precedence over a higher-level mention in `REQUIREMENTS_DISCOVERY.md`.

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

## Suggested Frontend Structure

```text
src/frontend/
  src/
    api/
    components/
    layouts/
    pages/
      auth/
      documents/
      ai/
      wiki/
      review/
      admin/
      dashboard/
    router/
    stores/
```

Recommended frontend choices:

- Vue with Vite
- Pinia for state if shared auth/session state is needed
- Vue Router
- Vitest for unit tests
- Playwright only if end-to-end smoke tests are added

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

1. Read `AI_HANDOFF.md`, `PHASE_STATUS.md`, `KNOWN_LIMITATIONS.md`, and the relevant spec files.
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
npm run build
npm test
```

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
