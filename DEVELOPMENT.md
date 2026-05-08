# Development Guide

This document gives an AI coding agent enough operational context to scaffold, run, and continue the project without asking for routine setup decisions.

## Source Of Truth

Use these files as the product and engineering source of truth:

- `REQUIREMENTS_DISCOVERY.md`: business context and confirmed MVP scope
- `ARCHITECTURE_MVP.md`: stack, modules, deployment direction
- `IMPLEMENTATION_PLAN.md`: milestone order and acceptance criteria
- `API_SPEC.md`: API surface
- `DATA_MODEL.md`: database model and vector payloads
- `UI_FLOW.md`: pages and role-based flows
- `RAG_AND_WIKI_FLOW.md`: indexing, retrieval, prompt behavior
- `CODING_RULES.md`: implementation rules and definition of done for tasks

If documents conflict, prefer the more specific implementation document. For example, API behavior in `API_SPEC.md` takes precedence over a higher-level mention in `REQUIREMENTS_DISCOVERY.md`.

## Implementation Assumptions

- Backend should be ASP.NET Core.
- Frontend should be Vue.
- SQLite is the metadata database for MVP.
- ChromaDB is the current development vector database.
- Qdrant remains the original target option and should stay behind a vector-store adapter boundary.
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

Work milestone by milestone:

1. Scaffold the minimal backend/frontend structure.
2. Add the smallest slice that satisfies the current milestone.
3. Add or update tests for the risky logic in that slice.
4. Run the relevant test/build commands.
5. Fix failures before starting the next milestone.
6. Keep README, `.env.example`, and this file current when commands or setup change.

Do not broaden scope beyond `IMPLEMENTATION_PLAN.md` unless required to make an accepted MVP flow work.

## Expected Commands After Scaffold

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

## Known Setup Gaps Before Scaffold

At the time this handoff file was created, the repository contains documentation only. The implementation still needs:

- Backend solution and API project
- Backend test project
- Frontend Vue project
- Chroma vector-store adapter
- Real runnable scripts
- Database migrations
- Seed data
- Smoke tests
