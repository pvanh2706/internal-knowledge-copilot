# Internal Knowledge Copilot

Internal Knowledge Copilot is an internal knowledge management MVP with document approval, RAG-based AI Q&A, feedback, wiki draft generation, and reviewer publishing.

The product and implementation scope are defined in:

- [AI_HANDOFF.md](AI_HANDOFF.md)
- [REQUIREMENTS_DISCOVERY.md](REQUIREMENTS_DISCOVERY.md)
- [ARCHITECTURE_MVP.md](ARCHITECTURE_MVP.md)
- [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)
- [API_SPEC.md](API_SPEC.md)
- [DATA_MODEL.md](DATA_MODEL.md)
- [UI_FLOW.md](UI_FLOW.md)
- [RAG_AND_WIKI_FLOW.md](RAG_AND_WIKI_FLOW.md)
- [CODING_RULES.md](CODING_RULES.md)
- [PHASE_STATUS.md](PHASE_STATUS.md)
- [KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md)
- [ROADMAP.md](ROADMAP.md)
- [SECURITY_CHECKLIST.md](SECURITY_CHECKLIST.md)
- [DEPLOYMENT_IIS.md](DEPLOYMENT_IIS.md)

## MVP Stack

- Backend: ASP.NET Core API
- Frontend: Vue
- Metadata database: SQLite
- Vector database for current development: ChromaDB
- Original target vector database: Qdrant, kept behind an adapter boundary for later replacement if needed
- File storage: local filesystem outside public web root
- Deployment target: Windows Server / IIS

## Project Structure

The current implementation uses this structure:

```text
src/
  backend/
    InternalKnowledgeCopilot.Api/
    InternalKnowledgeCopilot.Tests/
  frontend/
.env.example
```

## Local Development

See [DEVELOPMENT.md](DEVELOPMENT.md) for setup, configuration, and development workflow.

Local commands:

```powershell
chroma run --host localhost --port 8000 --path ./.chroma
dotnet restore src/backend/InternalKnowledgeCopilot.sln
dotnet run --project src/backend/InternalKnowledgeCopilot.Api
cd src/frontend
npm install
npm run dev
```

## Testing

See [TESTING.md](TESTING.md) for the test strategy.

Local verification commands:

```powershell
dotnet test src/backend/InternalKnowledgeCopilot.sln
cd src/frontend
npm test
npm run build
powershell -ExecutionPolicy Bypass -File ../../scripts/smoke-mvp.ps1
```

## Done Criteria

The MVP completion status is tracked in [PHASE_STATUS.md](PHASE_STATUS.md). The acceptance checklist is kept in [DONE_CRITERIA.md](DONE_CRITERIA.md).

## AI Working Instructions

An AI coding agent should:

1. Read [AI_HANDOFF.md](AI_HANDOFF.md) first.
2. Read this README.
3. Read the relevant spec files listed above.
4. Follow [CODING_RULES.md](CODING_RULES.md).
5. Check [PHASE_STATUS.md](PHASE_STATUS.md), [KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md), and [ROADMAP.md](ROADMAP.md) before changing scope.
6. After each implementation slice, run the relevant build/test commands from [TESTING.md](TESTING.md).
7. Continue fixing failures until the requested change is complete or a real blocker is found.

Ask the user only when blocked by missing credentials, missing product decisions, unavailable infrastructure, or contradictory requirements.

## Current Vector DB Decision

Use ChromaDB for development and test runs because it is available in this environment and does not require Docker. Keep vector operations behind an application interface so Qdrant can be added later without changing document processing or AI Q&A business flow.

## Deployment And Backup

Use [DEPLOYMENT_IIS.md](DEPLOYMENT_IIS.md) for Windows Server / IIS deployment notes and backup guidance. Use [SECURITY_CHECKLIST.md](SECURITY_CHECKLIST.md) as the MVP pilot readiness checklist.

## Presentation Materials

Use [docs/presentation](docs/presentation) for boss-facing presentation notes, slide outline, demo script, pilot plan, and demo checklist.
