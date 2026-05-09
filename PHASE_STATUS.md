# Phase Status

Last updated: 2026-05-09

- [x] Milestone 0 - Khoi tao project
- [x] Milestone 1 - Auth, user, role, team
- [x] Milestone 2 - Folder tree va phan quyen
- [x] Milestone 3 - Upload, review va versioning tai lieu
- [ ] Milestone 4 - Document processing va vector indexing
- [ ] Milestone 5 - AI Q&A co nguon
- [ ] Milestone 6 - Feedback va reviewer queue
- [ ] Milestone 7 - Wiki draft va publish
- [ ] Milestone 8 - Dashboard KPI va audit log
- [ ] Milestone 9 - Hardening MVP

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
