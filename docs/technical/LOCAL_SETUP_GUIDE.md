# Local Setup Guide

Tai lieu nay danh cho ky thuat moi clone repo ve may local de chay thu Internal Knowledge Copilot.

## 1. Muc tieu

Sau khi lam theo tai lieu nay, ban co the:

- Kiem tra moi truong can thiet.
- Clone source code.
- Cai dependency backend/frontend.
- Chay ChromaDB local.
- Chay ASP.NET Core API.
- Chay Vue frontend.
- Dang nhap bang seed account.
- Chay build, unit test va smoke test.

## 2. Yeu cau moi truong

Can cai cac cong cu sau:

- Git.
- .NET SDK phu hop voi project.
- Node.js va npm.
- Python va pip de cai ChromaDB CLI neu may chua co.
- ChromaDB CLI.
- PowerShell.

He dieu hanh duoc uu tien cho MVP:

- Windows 10/11 hoac Windows Server.

## 3. Kiem tra cong cu da cai

Mo PowerShell va chay:

```powershell
git --version
dotnet --info
node -v
npm -v
python --version
pip --version
chroma --help
```

Neu lenh nao khong chay duoc, cai cong cu tuong ung truoc khi tiep tuc.

## 4. Cai ChromaDB CLI

Neu `chroma --help` chua chay duoc, cai bang pip:

```powershell
pip install chromadb
```

Kiem tra lai:

```powershell
chroma --help
```

Neu Windows khong tim thay lenh `chroma`, dong va mo lai PowerShell. Neu van khong duoc, kiem tra Python Scripts folder da nam trong `PATH` chua.

## 5. Clone repository

Vi du:

```powershell
cd D:\MiniProject
git clone <repo-url> 13.InternalKnowledgeCopilot
cd 13.InternalKnowledgeCopilot
```

Thay `<repo-url>` bang URL repository thuc te.

## 6. Kiem tra cau truc source

Sau khi clone, repo nen co cac folder/file chinh:

```text
src/backend/InternalKnowledgeCopilot.sln
src/backend/InternalKnowledgeCopilot.Api
src/backend/InternalKnowledgeCopilot.Tests
src/frontend
scripts/smoke-mvp.ps1
.env.example
```

Kiem tra nhanh:

```powershell
Test-Path src/backend/InternalKnowledgeCopilot.sln
Test-Path src/frontend/package.json
Test-Path scripts/smoke-mvp.ps1
```

Tat ca nen tra ve `True`.

## 7. Cau hinh local

`.env.example` la danh sach bien moi truong tham chieu.

Trong local development, backend doc config tu `appsettings` va environment variables. Cach don gian nhat khi chay thu la dung cac gia tri mac dinh va chi set env khi can doi path/port.

Gia tri quan trong:

```text
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:5000
VITE_API_BASE_URL=http://localhost:5000/api
Database__SqlitePath=./data/internal-knowledge-copilot.db
Storage__RootPath=./storage
Chroma__BaseUrl=http://localhost:8000
AiProvider__Name=mock
Seed__Enabled=true
```

Luu y:

- Khong commit secret that.
- Local co the dung mock AI provider.
- `data`, `storage`, `.chroma`, `.run`, `logs` la runtime folders va da duoc ignore.

## 8. Restore va build backend

Tai root repo:

```powershell
dotnet restore src/backend/InternalKnowledgeCopilot.sln
dotnet build src/backend/InternalKnowledgeCopilot.sln
```

Neu build fail, xem `docs/technical/TROUBLESHOOTING.md`.

## 9. Cai frontend dependencies

```powershell
cd src/frontend
npm install
cd ../..
```

Neu muon dung dung lockfile de cai dependency on dinh hon:

```powershell
cd src/frontend
npm ci
cd ../..
```

## 10. Chay ChromaDB

Mo PowerShell terminal rieng tai root repo:

```powershell
chroma run --host localhost --port 8000 --path ./.chroma
```

Giu terminal nay dang chay.

Kiem tra ChromaDB:

```powershell
Invoke-RestMethod http://localhost:8000/api/v2/heartbeat
```

## 11. Chay backend API

Mo PowerShell terminal thu hai tai root repo.

De khop voi frontend config mac dinh, nen chay backend tai port `5000`:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:5000'
$env:Chroma__BaseUrl='http://localhost:8000'
dotnet run --project src/backend/InternalKnowledgeCopilot.Api --urls http://localhost:5000
```

Kiem tra API:

```powershell
Invoke-RestMethod http://localhost:5000/api/health
```

Neu ban chay bang launch profile mac dinh cua Visual Studio/Rider, port co the la `http://localhost:5291`. Khi do can cap nhat `VITE_API_BASE_URL` cua frontend tuong ung.

## 12. Chay frontend

Mo PowerShell terminal thu ba:

```powershell
cd src/frontend
$env:VITE_API_BASE_URL='http://localhost:5000/api'
npm run dev
```

Vite se hien URL local, thuong la:

```text
http://localhost:5173
```

Mo URL nay tren trinh duyet.

## 13. Tai khoan seed local

Neu `Seed__Enabled=true`, local development co cac account:

```text
Admin:
  email: admin@example.local
  password: ChangeMe123!

Reviewer:
  email: reviewer@example.local
  password: ChangeMe123!

User:
  email: user@example.local
  password: ChangeMe123!
```

Dung Admin de tao team/folder/quyen. Dung User de upload va hoi AI. Dung Reviewer de approve tai lieu va publish wiki.

## 14. Chay test va build

Backend:

```powershell
dotnet build src/backend/InternalKnowledgeCopilot.sln
dotnet test src/backend/InternalKnowledgeCopilot.sln
```

Frontend:

```powershell
cd src/frontend
npm run build
npm test
cd ../..
```

## 15. Chay smoke test day du

Smoke test tu dong chay luong MVP chinh voi port va thu muc tam rieng:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-mvp.ps1
```

Script nay se:

- Start ChromaDB tam thoi.
- Start API tam thoi.
- Dung SQLite/storage tam trong `%TEMP%`.
- Login seed accounts.
- Tao folder/quyen.
- Upload va approve document.
- Index document.
- Hoi AI co citation.
- Gui feedback.
- Generate va publish wiki.
- Kiem tra dashboard va audit log.

Truoc khi chay smoke:

- Dam bao `dotnet build` da pass.
- Dam bao `chroma` command chay duoc.
- Dong cac process dang chiem port smoke neu co.

## 16. Thu tu chay local hang ngay

Moi ngay khi dev:

1. Pull code moi.
2. Chay backend build/test neu co thay doi backend.
3. Chay frontend install neu `package-lock.json` thay doi.
4. Start ChromaDB.
5. Start backend API.
6. Start frontend.
7. Login bang seed account.

Lenh tom tat:

```powershell
git pull
dotnet build src/backend/InternalKnowledgeCopilot.sln
dotnet test src/backend/InternalKnowledgeCopilot.sln
Start-Process powershell -ArgumentList '-NoExit', '-Command', 'chroma run --host localhost --port 8000 --path ./.chroma'
dotnet run --project src/backend/InternalKnowledgeCopilot.Api --urls http://localhost:5000
```

Frontend chay o terminal rieng:

```powershell
cd src/frontend
npm run dev
```

## 17. Tai lieu lien quan

- `README.md`: tong quan repo.
- `DEVELOPMENT.md`: ghi chu phat trien.
- `TESTING.md`: chien luoc test va smoke flow.
- `.env.example`: bien cau hinh.
- `docs/technical/TROUBLESHOOTING.md`: loi thuong gap.
- `DEPLOYMENT_IIS.md`: ghi chu deploy IIS.

