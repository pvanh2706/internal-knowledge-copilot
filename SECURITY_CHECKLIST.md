# MVP Security Checklist

Use this checklist before marking the MVP ready for internal pilot.

## Authentication And Secrets

- [x] All business APIs require JWT authentication unless explicitly public, for example login.
- [x] Role-gated APIs use Admin, Reviewer, and User rules from the MVP scope.
- [x] JWT issuer, audience, signing key, and token lifetime are configurable.
- [x] Real JWT signing keys and AI provider keys must come from environment variables, user secrets, or server secret storage.
- [x] `.env.example` contains placeholders only.
- [x] Passwords and API keys are not written to application logs or audit logs.

## Authorization

- [x] Folder visibility is rechecked from SQLite for document list/detail/download and AI retrieval.
- [x] Users cannot fetch documents from folders they cannot view.
- [x] Reviewer-only actions are restricted to Reviewer where required by the MVP workflow.
- [x] Admin-only actions are restricted to Admin where required by the MVP workflow.
- [x] Audit log viewing is Admin-only.

## File Handling

- [x] Upload validation allows only PDF, DOCX, Markdown, and TXT.
- [x] Upload validation enforces `Storage__MaxUploadBytes`, default 20MB.
- [x] Uploaded files are stored under `Storage__RootPath`, outside the frontend/public web root.
- [x] Stored file names are generated server-side and sanitized.
- [x] Download always goes through `/api/documents/{id}/download` with authorization.
- [x] Download rejects stored paths that resolve outside `Storage__RootPath`.
- [x] Path traversal file names are covered by automated tests.

## RAG And Vector Retrieval

- [x] Approved/current document versions are rechecked from SQLite before being used as context.
- [x] Wiki chunks are rechecked from SQLite before being used as context.
- [x] Archived wiki pages are excluded.
- [x] Folder-scoped wiki pages require folder visibility.
- [x] Company-scoped wiki pages require explicit company publish confirmation.
- [x] AI citations are generated only from backend-accepted retrieved chunks.

## Audit And Logs

- [x] Major business actions write audit logs in SQLite.
- [x] Audit metadata avoids raw file content, passwords, and API keys.
- [x] Technical logs may include request failures but must not include secrets.

## Backup And Recovery

- [x] SQLite path is configurable.
- [x] Storage folder path is configurable.
- [x] Local backup script exists at `scripts/backup-local.ps1`.
- [x] ChromaDB index can be rebuilt from approved document versions and published wiki pages.
- [ ] Production backup schedule is configured by the server owner.
- [ ] Restore drill has been run on a separate machine before pilot data is considered protected.

## Manual Checks Before Pilot

- [ ] Run `dotnet test src/backend/InternalKnowledgeCopilot.sln`.
- [ ] Run `npm run build` and `npm test` in `src/frontend`.
- [ ] Run `scripts/smoke-mvp.ps1`.
- [ ] Confirm `Storage__RootPath` is not inside IIS static web content.
- [ ] Confirm production `Seed__Enabled=false` after initial admin account setup.
- [ ] Confirm server firewall only exposes intended IIS ports.
