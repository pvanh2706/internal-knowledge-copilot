# Phase Status

Last updated: 2026-05-09

- [x] Milestone 0 - Khởi tạo project
- [x] Milestone 1 - Auth, user, role, team
- [ ] Milestone 2 - Folder tree và phân quyền
- [ ] Milestone 3 - Upload, review và versioning tài liệu
- [ ] Milestone 4 - Document processing và vector indexing
- [ ] Milestone 5 - AI Q&A có nguồn
- [ ] Milestone 6 - Feedback và reviewer queue
- [ ] Milestone 7 - Wiki draft và publish
- [ ] Milestone 8 - Dashboard KPI và audit log
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
