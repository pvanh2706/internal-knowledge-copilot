# Troubleshooting

Tai lieu nay tong hop loi thuong gap khi clone va chay Internal Knowledge Copilot tren local.

## 1. `dotnet` khong duoc nhan dien

Trieu chung:

```text
dotnet : The term 'dotnet' is not recognized
```

Cach xu ly:

- Cai .NET SDK.
- Dong va mo lai PowerShell.
- Chay lai:

```powershell
dotnet --info
```

## 2. Backend build fail do package restore

Thu:

```powershell
dotnet restore src/backend/InternalKnowledgeCopilot.sln
dotnet build src/backend/InternalKnowledgeCopilot.sln
```

Neu van fail:

- Kiem tra ket noi internet.
- Kiem tra corporate proxy neu co.
- Xoa `bin/obj` cua project bi loi roi build lai.

## 3. `npm install` fail

Thu:

```powershell
cd src/frontend
npm ci
```

Neu van fail:

- Kiem tra Node.js/npm version:

```powershell
node -v
npm -v
```

- Xoa `node_modules` va cai lai:

```powershell
Remove-Item -Recurse -Force node_modules
npm install
```

Neu dung corporate proxy, can cau hinh npm proxy theo quy dinh noi bo.

## 4. `chroma` khong duoc nhan dien

Trieu chung:

```text
chroma : The term 'chroma' is not recognized
```

Cach xu ly:

```powershell
pip install chromadb
chroma --help
```

Neu van loi:

- Dong va mo lai PowerShell.
- Kiem tra Python Scripts path co nam trong `PATH` khong.
- Chay:

```powershell
python -m pip show chromadb
```

## 5. ChromaDB khong start duoc

Thu chay:

```powershell
chroma run --host localhost --port 8000 --path ./.chroma
```

Neu port bi chiem:

```powershell
netstat -ano | findstr :8000
```

Chon mot trong hai cach:

- Tat process dang chiem port.
- Doi port ChromaDB va cap nhat `Chroma__BaseUrl`.

Vi du:

```powershell
chroma run --host localhost --port 8001 --path ./.chroma
$env:Chroma__BaseUrl='http://localhost:8001'
```

## 6. Backend khong ket noi duoc ChromaDB

Trieu chung:

- API start duoc nhung indexing/AI Q&A fail.
- Log co loi ket noi `localhost:8000`.

Kiem tra:

```powershell
Invoke-RestMethod http://localhost:8000/api/v2/heartbeat
```

Neu fail:

- Start ChromaDB truoc.
- Kiem tra port.
- Kiem tra env var:

```powershell
echo $env:Chroma__BaseUrl
```

## 7. Frontend goi sai API port

Trieu chung:

- Login fail.
- Browser console co loi network/CORS.
- Frontend goi `localhost:5000` nhung backend dang chay `5291`, hoac nguoc lai.

Cach xu ly:

Chay backend co dinh port `5000`:

```powershell
dotnet run --project src/backend/InternalKnowledgeCopilot.Api --urls http://localhost:5000
```

Chay frontend voi API base URL tuong ung:

```powershell
cd src/frontend
$env:VITE_API_BASE_URL='http://localhost:5000/api'
npm run dev
```

Neu backend chay o `5291`:

```powershell
$env:VITE_API_BASE_URL='http://localhost:5291/api'
npm run dev
```

## 8. API health endpoint khong tra loi

Kiem tra backend co dang chay khong:

```powershell
Invoke-RestMethod http://localhost:5000/api/health
```

Neu fail:

- Xem terminal backend.
- Kiem tra port backend.
- Kiem tra database/storage path co quyen ghi khong.
- Thu build lai:

```powershell
dotnet build src/backend/InternalKnowledgeCopilot.sln
```

## 9. Login seed account fail

Kiem tra:

- Backend dang chay voi `ASPNETCORE_ENVIRONMENT=Development`.
- Seed enabled.
- Database local co duoc tao moi khong.

Neu database local da co data cu, co the seed khong tao lai password nhu mong doi. Trong local dev, co the dung database moi bang cach doi path:

```powershell
$env:Database__SqlitePath='./data/dev-new.db'
dotnet run --project src/backend/InternalKnowledgeCopilot.Api --urls http://localhost:5000
```

Hoac xoa database local cu neu khong can giu data:

```powershell
Remove-Item .\data\internal-knowledge-copilot.db
```

Chi xoa database local khi chac chan khong can du lieu trong do.

## 10. Upload file fail

Kiem tra:

- Dinh dang file co duoc ho tro khong: PDF, DOCX, Markdown, TXT.
- File co vuot 20MB khong.
- `Storage__RootPath` co quyen ghi khong.
- Folder upload co ton tai va user co quyen khong.

## 11. Document approved nhung AI khong tim thay

Kiem tra:

- Reviewer da approve dung version chua.
- Processing/indexing da xong chua.
- ChromaDB dang chay chua.
- User co quyen folder/document khong.
- Cau hoi co lien quan ro den noi dung tai lieu khong.

Thu hoi voi scope Document de giam nhieu:

```text
Chon document cu the -> dat cau hoi ro hon
```

## 12. Smoke test fail

Chay:

```powershell
dotnet build src/backend/InternalKnowledgeCopilot.sln
powershell -ExecutionPolicy Bypass -File scripts\smoke-mvp.ps1
```

Neu fail:

- Doc error trong terminal.
- Kiem tra `chroma` command co chay duoc khong.
- Kiem tra port smoke co bi chiem khong: API `5080`, Chroma `8040`.

```powershell
netstat -ano | findstr :5080
netstat -ano | findstr :8040
```

Script smoke dung thu muc tam trong `%TEMP%`, nen thuong khong anh huong database local cua ban.

## 13. PowerShell chan script

Trieu chung:

```text
running scripts is disabled on this system
```

Chay smoke bang:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-mvp.ps1
```

Neu policy cong ty chan manh hon, lien he IT/admin noi bo.

## 14. Port bi chiem

Kiem tra process dang chiem port:

```powershell
netstat -ano | findstr :5000
netstat -ano | findstr :5173
netstat -ano | findstr :8000
```

Dung Task Manager hoac `Stop-Process` voi PID neu ban chac chan process do co the tat.

Vi du:

```powershell
Stop-Process -Id <PID>
```

## 15. Khi nao nen bao nguoi maintain

Bao nguoi maintain neu:

- Build fail sau khi clone sach va restore thanh cong.
- Smoke test fail lap lai.
- Co nghi ngo loi phan quyen bao mat.
- AI dung nguon ma user khong co quyen.
- Tai lieu rejected/pending van xuat hien trong Q&A.
- Loi can sua code, khong phai loi setup local.

