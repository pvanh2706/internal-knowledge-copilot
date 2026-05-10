# Roadmap

Last updated: 2026-05-10

This roadmap is a practical backlog for post-MVP improvement. Treat it as guidance, not a committed delivery plan.

## v1.1 Candidates

- Add a real production AI provider configuration and operational runbook.
- Improve keyword/full-text search, starting with SQLite FTS if the dataset stays small.
- Add PDF/text preview in the document detail screen.
- Add a basic wiki editor before publish.
- Add wiki versioning.
- Generate wiki drafts from a folder or selected set of documents.
- Add in-app notifications for review queues.
- Expand frontend tests around document review, AI feedback, and wiki publish flows.
- Add Playwright end-to-end tests for the main user journeys.
- Improve dashboard KPIs based on pilot feedback.

## v1.2 Candidates

- Add SSO integration.
- Add SQL Server or PostgreSQL support for metadata.
- Move background jobs to a dedicated worker if processing becomes slow or long-running.
- Add malware scanning for uploaded files.
- Add secret scanning or redaction for uploaded content before indexing.
- Add better observability: structured logs, health checks, and operational dashboards.
- Add backup/restore automation for SQLite, storage, and vector rebuild.

## v2 Candidates

- Replace ChromaDB with Qdrant if production requirements call for it.
- Add Elasticsearch or another search engine if keyword search becomes a core workflow.
- Add knowledge graph or business rule catalog features.
- Add codebase/API/workflow understanding as a new knowledge source type.
- Add multi-tenant or department-isolated deployments if needed.

## Good First Future Tasks

- Update old Qdrant wording in legacy planning docs to say ChromaDB is the current implementation and Qdrant is the future option.
- Add a short production AI provider setup guide once a provider is chosen.
- Add a script that verifies documentation links and common local prerequisites.

