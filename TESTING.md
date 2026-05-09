# Testing Guide

This document defines how the project should be tested as it is implemented.

## Test Philosophy

Prioritize tests around business rules, permissions, document state transitions, RAG filtering, and security-sensitive flows. UI testing can stay lighter for MVP, but the main user flows must have smoke coverage before the project is considered complete.

## Backend Tests

Backend tests should cover at minimum:

- Permission service for folder, document, and wiki visibility
- User role checks for Admin, Reviewer, and User
- Login and password change behavior
- Document upload validation
- Document approval and rejection
- Document versioning rules
- Processing job creation after approval
- Vector search filter construction by user permission and scope
- AI answer behavior when no relevant context exists
- AI feedback creation and reviewer status updates
- Wiki draft publish rules, especially company-wide confirmation
- Dashboard metric calculations
- Audit log creation for major business actions

Expected command after scaffold:

```powershell
dotnet test src/backend/InternalKnowledgeCopilot.sln
```

## Frontend Tests

Frontend tests should cover at minimum:

- Login form validation
- Role-based navigation visibility
- Document list empty/loading/error states
- Upload form validation for file type and 20MB limit
- AI ask form scope selection
- Feedback submit controls
- Reviewer approve/reject form behavior
- Wiki publish confirmation behavior

Expected commands after scaffold:

```powershell
cd src/frontend
npm test
npm run build
```

## Smoke Test Flow

Before the MVP is marked complete, verify this end-to-end flow locally:

1. Start ChromaDB.
2. Start backend.
3. Start frontend.
4. Login as Admin.
5. Create or verify teams and users.
6. Create folders and assign folder permissions.
7. Login as User.
8. Upload a valid TXT or Markdown document.
9. Login as Reviewer.
10. Approve the document.
11. Confirm processing/indexing succeeds.
12. Login as User.
13. Ask AI a question in the allowed scope.
14. Confirm the answer includes citations.
15. Submit incorrect feedback.
16. Login as Reviewer.
17. Confirm incorrect feedback appears in the reviewer queue.
18. Generate a wiki draft from an approved document.
19. Publish the wiki.
20. Ask AI again and confirm published wiki is preferred when relevant.
21. Login as Admin or Reviewer and confirm dashboard metrics update.

## Build Gates

The project should not be considered ready if any of these fail:

```powershell
dotnet build src/backend/InternalKnowledgeCopilot.sln
dotnet test src/backend/InternalKnowledgeCopilot.sln
cd src/frontend
npm run build
npm test
powershell -ExecutionPolicy Bypass -File ../../scripts/smoke-mvp.ps1
```

If some command is not available yet because the project has not been scaffolded, the AI agent should create the missing project/script as part of milestone 0.

## Manual Security Checks

Verify these manually during hardening:

- Uploaded files are not served from public web root.
- Download always goes through an authorized API.
- Path traversal is blocked for upload/download.
- Rejected documents are never indexed.
- Pending documents are never used for AI Q&A or wiki generation.
- Vector search cannot return unauthorized chunks.
- AI cannot invent citations that were not retrieved by backend.
- Secrets and passwords are not logged.

## MVP Smoke Script

The final local MVP smoke script is:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-mvp.ps1
```

It starts temporary ChromaDB and API instances, uses a temporary SQLite database and storage folder, then verifies Admin setup, folder permission, User upload, Reviewer approval, indexing, AI Q&A, feedback queue, wiki publish, dashboard metrics, and audit logs.
