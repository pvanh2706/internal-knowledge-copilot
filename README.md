# Internal Knowledge Copilot

Internal Knowledge Copilot is an internal knowledge management MVP with document approval, RAG-based AI Q&A, feedback, wiki draft generation, and reviewer publishing.

The product and implementation scope are defined in:

- [REQUIREMENTS_DISCOVERY.md](REQUIREMENTS_DISCOVERY.md)
- [ARCHITECTURE_MVP.md](ARCHITECTURE_MVP.md)
- [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)
- [API_SPEC.md](API_SPEC.md)
- [DATA_MODEL.md](DATA_MODEL.md)
- [UI_FLOW.md](UI_FLOW.md)
- [RAG_AND_WIKI_FLOW.md](RAG_AND_WIKI_FLOW.md)
- [CODING_RULES.md](CODING_RULES.md)
- [PHASE_STATUS.md](PHASE_STATUS.md)

## MVP Stack

- Backend: ASP.NET Core API
- Frontend: Vue
- Metadata database: SQLite
- Vector database for current development: ChromaDB
- Original target vector database: Qdrant, kept behind an adapter boundary for later replacement if needed
- File storage: local filesystem outside public web root
- Deployment target: Windows Server / IIS

## Expected Project Structure

The implementation should be scaffolded toward this structure:

```text
src/
  backend/
    InternalKnowledgeCopilot.Api/
    InternalKnowledgeCopilot.Tests/
  frontend/
docker-compose.yml
.env.example
```

## Local Development

See [DEVELOPMENT.md](DEVELOPMENT.md) for setup, configuration, and development workflow.

Expected commands after scaffold:

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

Expected commands after scaffold:

```powershell
dotnet test src/backend/InternalKnowledgeCopilot.sln
cd src/frontend
npm test
npm run build
```

## Done Criteria

The MVP is not considered complete until the checklist in [DONE_CRITERIA.md](DONE_CRITERIA.md) passes.

## AI Working Instructions

An AI coding agent should:

1. Read this README first.
2. Read the spec files listed above.
3. Follow [CODING_RULES.md](CODING_RULES.md).
4. Implement milestone by milestone from [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).
5. After each implementation slice, run the relevant build/test commands from [TESTING.md](TESTING.md).
6. Continue fixing failures until the done criteria pass or a real blocker is found.

Ask the user only when blocked by missing credentials, missing product decisions, unavailable infrastructure, or contradictory requirements.

## Current Vector DB Decision

Use ChromaDB for development and test runs because it is available in this environment and does not require Docker. Keep vector operations behind an application interface so Qdrant can be added later without changing document processing or AI Q&A business flow.
