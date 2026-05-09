# Phase Status

Last updated: 2026-05-10

- [x] Milestone 0 - Khoi tao project
- [x] Milestone 1 - Auth, user, role, team
- [x] Milestone 2 - Folder tree va phan quyen
- [x] Milestone 3 - Upload, review va versioning tai lieu
- [x] Milestone 4 - Document processing va vector indexing
- [x] Milestone 5 - AI Q&A co nguon
- [x] Milestone 6 - Feedback va reviewer queue
- [x] Milestone 7 - Wiki draft va publish
- [x] Milestone 8 - Dashboard KPI va audit log
- [x] Milestone 9 - Hardening MVP

## Completed Notes

### Milestone 0

- Scaffolded ASP.NET Core API and xUnit test project.
- Scaffolded Vue 3 + TypeScript frontend with router, Pinia, layout, and login placeholder.
- Added SQLite DbContext, initial EF migration, configuration options, and health endpoint.
- Updated development docs to use ChromaDB as the current vector database.
- Verified backend build/test and frontend build/test pass.

### Milestone 1

- Added users and teams schema with EF migration.
- Added development seed accounts for Admin, Reviewer, and User.
- Added password hashing, JWT login, password change, Admin user management, and team APIs.
- Added Vue login, password change, user management, and team management screens.
- Verified backend build/test, frontend build/test, and a backend smoke login pass.

### Milestone 2

- Added folders, folder permissions, and user folder permissions schema with EF migration.
- Added folder permission service for Admin/Reviewer full visibility and User team/user-scoped visibility.
- Added folder tree, folder detail, CRUD, soft delete, and team permission APIs.
- Added Vue folder management screen for Admin/Reviewer with create, edit, delete, and team permission controls.
- Verified backend build/test, frontend build/test, and a backend folder permission smoke flow pass.

### Milestone 3

- Added documents and document versions schema with EF migration.
- Added file upload validation for PDF, DOCX, Markdown, and TXT with 20MB limit.
- Added local file storage outside web root under the configured storage root.
- Added document upload, upload new version, list, detail, download, approve, and reject APIs.
- Added versioning behavior where pending/rejected new versions do not replace the current approved version.
- Added Vue document upload/list/detail/download page and reviewer approval queue.
- Verified backend build/test, frontend build/test, and a document upload/review/versioning/download smoke flow pass.

### Milestone 4

- Added processing job schema with EF migration and hosted worker.
- Added TXT, Markdown, DOCX, and PDF text extraction.
- Added deterministic mock embeddings for local testing without a real AI provider.
- Added text chunking and ChromaDB vector upsert with document/folder/version metadata.
- Enqueued document processing when a reviewer approves a document version.
- Added extracted text path, text hash, indexed timestamp, and processing failure status handling.
- Exposed latest version indexing status in the document list UI.
- Added a repeatable Chroma/API smoke script at `scripts/smoke-milestone4.ps1`.
- Verified backend build/test, frontend build/test, and a Chroma-backed upload/approve/index smoke flow pass.

### Milestone 5

- Added AI interaction and interaction source schema with EF migration.
- Added `/api/ai/ask` with All, Folder, and Document scopes.
- Added Chroma vector query support and backend permission filtering against SQLite folder/document access.
- Added mock Vietnamese answer generation with clarification fallback when retrieved context is insufficient.
- Stored each AI interaction with used source records for later feedback and dashboard milestones.
- Added a Vue AI Q&A page with scope selection, answer display, and citations.
- Added backend tests for clarification behavior and permission-filtered retrieval.
- Added a repeatable Chroma/API smoke script at `scripts/smoke-milestone5.ps1`.
- Verified backend build/test, frontend build/test, and a Chroma-backed upload/approve/index/ask smoke flow pass.

### Milestone 6

- Added AI feedback schema with EF migration.
- Added feedback submission for AI interactions with Correct and Incorrect values.
- Added reviewer queue API for incorrect feedback and review status updates.
- Added review statuses New, InReview, and Resolved.
- Added feedback controls under AI answers in the Vue Q&A page.
- Added a reviewer feedback queue page with sources, user note, AI answer, and status update form.
- Added backend tests for incorrect feedback creation and reviewer resolution.
- Added a repeatable Chroma/API smoke script at `scripts/smoke-milestone6.ps1`.
- Verified backend build/test, frontend build/test, and a Chroma-backed ask/feedback/reviewer-update smoke flow pass.

### Milestone 7

- Added wiki draft and wiki page schema with EF migration.
- Added mock wiki draft generation from indexed approved document text.
- Added reviewer APIs to generate, list, view, publish, and reject wiki drafts.
- Added wiki publish validation for folder/company visibility and company public confirmation.
- Indexed published wiki pages into ChromaDB with wiki metadata for Q&A retrieval.
- Added frontend wiki draft review page and a generate wiki draft action on indexed approved documents.
- Updated Q&A flow verification so published wiki chunks are prioritized over document chunks.
- Added backend tests for draft generation and wiki publish indexing.
- Added a repeatable Chroma/API smoke script at `scripts/smoke-milestone7.ps1`.
- Verified backend build/test, frontend build/test, and a Chroma-backed document/index/wiki-publish/ask smoke flow pass.

### Milestone 8

- Added audit log schema with EF migration and an audit log service.
- Recorded audit events for folder, permission, document, wiki, AI feedback, team, and user management actions.
- Added Admin audit log API with date, action, and entity type filters.
- Added Reviewer/Admin dashboard summary API with document counts, wiki counts, AI question counts, feedback KPIs, pending incorrect feedback count, and top cited sources.
- Added Vue dashboard KPI page and Admin audit log page.
- Added a repeatable Chroma/API smoke script at `scripts/smoke-milestone8.ps1`.
- Verified backend build/test, frontend build/test, and a Chroma-backed dashboard/audit smoke flow pass.

### Milestone 9

- Hardened file download so stored paths must resolve under the configured storage root.
- Hardened RAG retrieval so wiki chunks from ChromaDB are rechecked against SQLite before being used as AI context.
- Added backend tests for path traversal-safe storage and SQLite-confirmed wiki retrieval.
- Added final MVP smoke script at `scripts/smoke-mvp.ps1`.
- Added local backup script at `scripts/backup-local.ps1`.
- Added `SECURITY_CHECKLIST.md` and `DEPLOYMENT_IIS.md` for MVP security, backup, local smoke, and IIS deployment readiness.
- Verified backend build/test, frontend build/test, and the final Chroma-backed MVP smoke flow pass.
