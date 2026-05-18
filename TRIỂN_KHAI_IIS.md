# IIS Deployment And Backup Guide

This guide is for the MVP Windows Server / IIS target.

## Prerequisites

- .NET 8 Hosting Bundle installed on the server.
- IIS with ASP.NET Core Hosting Module.
- A writable application data directory outside `wwwroot`, for example `D:\IKCData`.
- ChromaDB reachable from the API server.
- Environment variables or server secrets for JWT and AI provider configuration.

## Build Artifacts

Backend:

```powershell
dotnet publish src/backend/InternalKnowledgeCopilot.Api/InternalKnowledgeCopilot.Api.csproj -c Release -o ./.publish/api
```

Frontend:

```powershell
cd src/frontend
npm install
npm run build
```

Deploy the API publish folder to the IIS site path. Deploy frontend `dist` either as a separate static IIS site or behind the same reverse proxy setup used by the team.

## Required Server Configuration

Set these variables on the IIS app pool or server environment:

```powershell
ASPNETCORE_ENVIRONMENT=Production
Database__SqlitePath=D:\IKCData\db\internal-knowledge-copilot.db
Storage__RootPath=D:\IKCData\storage
Jwt__Issuer=InternalKnowledgeCopilot
Jwt__Audience=InternalKnowledgeCopilot
Jwt__SigningKey=<server-secret-at-least-32-chars>
Jwt__AccessTokenMinutes=120
Chroma__BaseUrl=http://localhost:8000
Chroma__Collection=knowledge_chunks
Seed__Enabled=false
```

Keep `Storage__RootPath` outside the IIS static site directory.

## Database Migration

The API applies EF Core migrations at startup. The IIS app pool identity needs write access to:

- The SQLite database directory.
- The storage directory.
- The API log/output directory if configured by IIS.

## ChromaDB

Run ChromaDB as a Windows service or supervised background process. For local-style operation:

```powershell
chroma run --host localhost --port 8000 --path D:\IKCData\chroma
```

Do not expose ChromaDB directly to end users.

## Backup

For a simple stopped-app backup:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\backup-local.ps1 `
  -DatabasePath D:\IKCData\db\internal-knowledge-copilot.db `
  -StoragePath D:\IKCData\storage `
  -BackupRoot D:\IKCBackups
```

Operational notes:

- Stop the API or schedule backups during low traffic for SQLite file consistency.
- Back up SQLite and storage together.
- ChromaDB can be backed up by copying its data directory while stopped, but the MVP recovery strategy is to rebuild vector data from approved document versions and published wiki pages.
- Store backups outside the application server when possible.
- Test restore on a separate machine before pilot launch.

## Smoke Verification After Deploy

From a machine that can reach the server:

1. Login as Admin.
2. Create or verify a folder and team permission.
3. Login as User and upload a TXT file.
4. Login as Reviewer and approve it.
5. Confirm document processing reaches `Indexed`.
6. Ask AI and verify citations.
7. Submit incorrect feedback and verify Reviewer queue.
8. Generate and publish a wiki draft.
9. Confirm dashboard metrics and audit logs update.

For local verification, run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-mvp.ps1
```
