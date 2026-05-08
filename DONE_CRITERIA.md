# MVP Done Criteria

The MVP is complete only when every required item below is done or explicitly documented as deferred.

## Project Setup

- Backend ASP.NET Core project exists.
- Backend test project exists.
- Frontend Vue project exists.
- ChromaDB can run locally for development and testing.
- SQLite database is created through migrations.
- `.env.example` documents all required configuration.
- README includes current setup, run, build, and test commands.
- Development seed data exists for Admin, Reviewer, User, and the two MVP teams.

## Core Backend

- Auth login works.
- First-login password change works.
- Admin can create and update users.
- Admin can manage teams.
- Admin or Reviewer can manage folders.
- Folder permissions are enforced.
- User cannot see unauthorized folders/documents.
- User can upload PDF, DOCX, Markdown, and TXT files.
- Upload validation enforces allowed file types and 20MB max size.
- Uploaded files are stored outside public web root.
- Reviewer can approve or reject documents.
- Reject reason is required.
- Versioning keeps the old current version active until the new version is approved and indexed.
- Approved document versions create processing jobs.
- Processing jobs extract, chunk, embed, and index content.
- Processing errors are visible in document status.

## RAG And AI

- ChromaDB collection is created or verified automatically.
- Only approved/current document chunks are indexed.
- Only published wiki chunks are indexed.
- AI Q&A supports All, Folder, and Document scopes.
- Retrieval respects folder/document/wiki permissions.
- Backend rechecks permissions before using retrieved chunks in prompts.
- AI answers in Vietnamese.
- AI asks for clarification when context is insufficient.
- AI responses include citations from retrieved sources.
- AI does not invent citations.
- Published wiki is preferred over document chunks when relevant.

## Wiki

- Reviewer can generate wiki draft from approved document.
- Wiki draft uses only source document content.
- Reviewer can publish or reject wiki draft.
- Reject reason is required.
- Company-wide publish requires explicit confirmation.
- Published wiki is indexed into the configured vector database.

## Feedback And Dashboard

- User can mark AI answer as Correct or Incorrect.
- User can add feedback notes.
- Incorrect feedback appears in Reviewer queue.
- Reviewer can update feedback review status.
- Dashboard shows MVP metrics for Admin and Reviewer.
- Audit logs are written for major business actions.

## Frontend

- Login page works.
- Password change page works.
- Role-based navigation works.
- Admin user/team/folder management screens work.
- Document list, upload, download, and status views work.
- Reviewer document review queue works.
- AI Q&A page works.
- Feedback controls work.
- Wiki draft list/detail/publish/reject screens work.
- Dashboard screen works.
- Empty, loading, and error states are implemented for main pages.
- UI text is Vietnamese.

## Verification

- Backend build passes.
- Backend tests pass.
- Frontend build passes.
- Frontend tests pass.
- Main smoke test flow in `TESTING.md` passes.
- No real secrets are committed.
- README and development docs match the actual commands.

## Deployment Readiness

- App can be configured for Windows Server / IIS deployment.
- SQLite file location is configurable.
- Storage folder location is configurable and outside web root.
- ChromaDB endpoint is configurable.
- AI provider keys are configured through environment variables or server secrets.
- Backup notes exist for SQLite, storage folder, and vector index rebuild strategy.
