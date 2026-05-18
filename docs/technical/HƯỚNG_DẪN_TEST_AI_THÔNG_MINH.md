# Smart AI Test Guide

Cap nhat: 2026-05-13

Tai lieu nay dung de test du an Internal Knowledge Copilot theo huong nghiep vu va chung minh he thong AI da thong minh hon so voi MVP hoi-dap don gian. Muc tieu khong chi la "API chay duoc", ma la thay duoc AI:

- Chi dung tai lieu da approve.
- Tra loi co citation kiem chung duoc.
- Ton trong quyen truy cap folder/team/user.
- Hieu cau truc tai lieu: section, summary, topics, entities.
- Biet noi khi thieu thong tin.
- Uu tien wiki/correction khi da duoc reviewer approve.
- Co feedback loop va evaluation de do chat luong cai thien.
- Co retrieval explain de reviewer nhin thay vi sao AI chon nguon.

## 1. Chuan bi moi truong

Tai root repo:

```powershell
dotnet build src/backend/InternalKnowledgeCopilot.sln
dotnet test src/backend/InternalKnowledgeCopilot.sln
cd src/frontend
npm ci
npm run build
npm test
cd ../..
```

Start 3 thanh phan local:

```powershell
chroma run --host localhost --port 8000 --path ./.chroma
dotnet run --project src/backend/InternalKnowledgeCopilot.Api --urls http://localhost:5000
cd src/frontend
$env:VITE_API_BASE_URL='http://localhost:5000/api'
npm run dev
```

URL mac dinh:

- Frontend: `http://localhost:5173`
- Backend API: `http://localhost:5000`
- ChromaDB: `http://localhost:8000`

Kiem tra nhanh:

```powershell
Invoke-RestMethod http://localhost:5000/api/health
Invoke-RestMethod http://localhost:8000/api/v2/heartbeat
```

## 2. Tai khoan test

Neu seed data dang bat, dung cac tai khoan sau:

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

Quyen ky vong:

- Admin: quan ly user/team/folder, xem dashboard/audit/index.
- Reviewer: approve document, xu ly feedback, tao correction, publish wiki, chay evaluation.
- User: upload document, hoi AI, gui feedback.

## 3. Bo tai lieu mau de test

Tao 2 file Markdown local de upload.

File 1: `support-refund-policy.md`

```markdown
# Quy trinh hoan tien don hang

## Muc dich

Tai lieu nay huong dan team Ho tro khach hang xu ly yeu cau hoan tien.

## Dieu kien hoan tien

Khach hang duoc hoan tien khi don hang bi huy truoc khi giao, san pham loi do nha cung cap, hoac dich vu khong duoc kich hoat sau 24 gio.

## SLA xu ly

Yeu cau hoan tien muc uu tien cao phai duoc phan hoi trong 4 gio lam viec.
Yeu cau thong thuong phai duoc phan hoi trong 1 ngay lam viec.

## Canh bao

Khong hoan tien truc tiep neu giao dich co dau hieu gian lan. Chuyen case cho Reviewer hoac Fraud team.

## Tu khoa nghiep vu

Refund-SLA-4H la ma noi bo cho SLA phan hoi hoan tien muc uu tien cao.
```

File 2: `support-vnpt-invoice-guide.md`

```markdown
# Huong dan hoa don dien tu VNPT

## Pham vi

Tai lieu nay huong dan team Ho tro khach hang kiem tra trang thai hoa don dien tu VNPT.

## Buoc kiem tra

1. Kiem tra ma so thue cua khach hang.
2. Kiem tra ma giao dich tren cong VNPT.
3. Neu trang thai la PendingSync qua 30 phut, tao ticket cho team Ky thuat.

## Thoi gian phan hoi

Case VNPT-PendingSync phai duoc phan hoi trong 2 gio lam viec.

## Luu y

Khong gui token tich hop VNPT cho khach hang qua email hoac chat.
```

Tai lieu mau co chu y:

- Co heading de test section detection.
- Co keyword chinh xac `Refund-SLA-4H`, `VNPT-PendingSync` de test keyword fallback/rerank.
- Co quy tac bao mat de test cau hoi co nguon.
- Co du lieu rieng tung chu de de test retrieval dung tai lieu.

## 4. Kich ban A - Smoke test ky thuat

Chay smoke script:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-mvp.ps1
```

Dat khi:

- Script start duoc ChromaDB va API tam thoi.
- Login seed accounts thanh cong.
- Upload, approve, index document thanh cong.
- AI Q&A tra loi co citation.
- Feedback Incorrect vao queue.
- Wiki publish duoc.
- Dashboard va audit log co du lieu.

Neu smoke fail, xem log script truoc khi test manual.

## 5. Kich ban B - Document ingestion thong minh

Muc tieu: chung minh he thong khong chi luu file, ma hieu cau truc va metadata tai lieu.

Buoc test:

1. Dang nhap bang `user@example.local`.
2. Upload `support-refund-policy.md` vao folder user co quyen.
3. Dang nhap bang `reviewer@example.local`.
4. Approve document.
5. Cho background job index xong.
6. Mo document detail/version detail.

Ky vong:

- Document status la `Approved` hoac version status la `Indexed`.
- Version co normalized text.
- UI hien section count, summary hoac document metadata neu da index xong.
- Citation sau nay co `sectionTitle`, khong chi co ten file.
- Neu tai lieu co warning chat luong, reviewer thay duoc warning.

Bang chung "AI thong minh hon":

- He thong phan tach tai lieu theo section.
- He thong tao summary/metadata, topics, entities, sensitivity neu provider ho tro.
- Chunk dua vao retrieval co metadata giau hon: title, folder, section, status, source type.

## 6. Kich ban C - Q&A co citation va khong bia

Muc tieu: chung minh cau tra loi dua tren nguon da approve.

Buoc test:

1. Dang nhap bang `user@example.local`.
2. Mo trang AI Q&A.
3. Hoi:

```text
Yeu cau hoan tien muc uu tien cao phai phan hoi trong bao lau?
```

Ky vong:

- AI tra loi: `4 gio lam viec` hoac noi dung tuong duong.
- Cau tra loi co citation ve document `Quy trinh hoan tien don hang`.
- Citation tro ve dung section `SLA xu ly` hoac section lien quan.
- Confidence khong phai luc nao cung high neu context yeu, nhung phai co ly do nguon.

Hoi tiep cau khong co trong tai lieu:

```text
Phi hoan tien quoc te cua cong ty la bao nhieu phan tram?
```

Ky vong:

- AI khong tu bia so phan tram.
- AI noi thieu thong tin hoac can bo sung tai lieu.
- Missing information hien ro neu UI co truong nay.

Bang chung "AI thong minh hon":

- Cau dung thi co nguon.
- Cau thieu nguon thi AI noi khong du thong tin.
- Citation duoc backend validate, khong phai LLM tu tao nguon.

## 7. Kich ban D - Hybrid retrieval va keyword chinh xac

Muc tieu: chung minh AI tim duoc ma noi bo/keyword kho ma embedding co the bo sot.

Can upload va approve ca 2 file mau.

Cau hoi 1:

```text
Refund-SLA-4H nghia la gi?
```

Ky vong:

- AI lay dung document refund.
- Tra loi day la ma noi bo cho SLA phan hoi hoan tien muc uu tien cao.
- Citation dung section `Tu khoa nghiep vu`.

Cau hoi 2:

```text
Neu VNPT-PendingSync qua 30 phut thi lam gi?
```

Ky vong:

- AI lay dung document hoa don dien tu VNPT.
- Tra loi tao ticket cho team Ky thuat.
- Citation dung section `Buoc kiem tra`.

Bang chung "AI thong minh hon":

- Retrieval ket hop vector va keyword index.
- Exact keyword/entity duoc boost trong reranking.
- Chunk co keyword chinh xac co the vuot chunk gan vector hon nhung lech noi dung.

## 8. Kich ban E - Permission-aware retrieval

Muc tieu: chung minh AI khong lay tai lieu trai quyen.

Buoc test:

1. Admin tao 2 folder, vi du `Support` va `Engineering`.
2. Gan user chi co quyen xem folder `Support`.
3. Upload tai lieu VNPT vao `Engineering` va approve/index.
4. Dang nhap user chi co quyen `Support`.
5. Hoi:

```text
VNPT-PendingSync qua 30 phut thi lam gi?
```

Ky vong:

- Neu user khong co quyen folder `Engineering`, AI khong duoc tra loi bang noi dung tai lieu do.
- AI noi khong co du thong tin trong pham vi duoc phep.
- Khong lo title/folder/section cua tai lieu trai quyen.

Bang chung "AI thong minh hon":

- Retrieval filter theo visible folder truoc khi lay context.
- Backend revalidate chunk bang SQLite truoc khi dua vao prompt.
- Vector DB khong phai source of truth cho permission.

## 9. Kich ban F - Retrieval explain cho Reviewer/Admin

Muc tieu: reviewer nhin thay vi sao AI chon nguon.

Buoc test:

1. Dang nhap bang Reviewer hoac Admin.
2. Mo trang Retrieval Explain neu UI co route tu menu.
3. Nhap cau hoi:

```text
Refund-SLA-4H nghia la gi?
```

Ky vong:

- Thay query understanding: keywords/entities duoc rut ra.
- Thay filter theo scope/folder.
- Thay candidate tu vector va keyword.
- Thay score reasons: keyword match, source priority, scope match, distance.
- Thay final context da chon.
- Explain khong tao ban ghi AI interaction moi.

Bang chung "AI thong minh hon":

- He thong co kha nang giai thich retrieval, khong phai hop den.
- Reviewer debug duoc case AI tra loi yeu ma khong doc log backend.

## 10. Kich ban G - Wiki intelligence

Muc tieu: chung minh AI khong chi copy file goc ma tao wiki co cau truc va lien ket tai lieu.

Buoc test:

1. Dang nhap Reviewer.
2. Chon document `Quy trinh hoan tien don hang` da approve/index.
3. Generate wiki draft.
4. Mo wiki draft.

Ky vong:

- Draft co title va noi dung co cau truc.
- Co missing information neu tai lieu goc thieu thong tin.
- Co related documents neu co tai lieu lien quan trong kho tri thuc.
- Reviewer co the publish sau khi kiem tra.

Sau khi publish:

1. Dang nhap User.
2. Hoi lai cau lien quan refund.

Ky vong:

- AI co the uu tien wiki published khi relevant.
- Citation co the den tu source type `Wiki`.

Bang chung "AI thong minh hon":

- Wiki sinh tu structured output.
- Wiki co related documents/missing information.
- Published wiki tro thanh nguon tri thuc uu tien hon document thuan.

## 11. Kich ban H - Feedback loop va correction

Muc tieu: chung minh he thong co vong cai thien sau feedback sai, nhung van co reviewer approve.

Buoc test:

1. User hoi mot cau ve refund.
2. Danh dau cau tra loi `Incorrect`.
3. Ghi note:

```text
Can nhan manh khong hoan tien truc tiep neu co dau hieu gian lan.
```

4. Reviewer mo feedback review/quality issue.
5. Reviewer tao correction draft.
6. Reviewer approve correction.
7. Cho correction duoc index.
8. User hoi lai:

```text
Khi nao khong duoc hoan tien truc tiep?
```

Ky vong:

- Feedback Incorrect tao quality issue.
- Correction chi co tac dung sau khi Reviewer approve.
- Lan hoi sau AI uu tien correction neu correction lien quan.
- Citation co source type `Correction` hoac noi dung correction duoc dung ro rang.

Bang chung "AI thong minh hon":

- He thong hoc tu feedback theo co che co kiem soat.
- AI khong tu sua tri thuc neu chua co reviewer approve.
- Retrieval uu tien approved correction truoc wiki/document.

## 12. Kich ban I - Evaluation before/after

Muc tieu: co so lieu chung minh chat luong cai thien.

Buoc test:

1. Reviewer tao eval case tu feedback sai hoac tao manual case.
2. Expected answer nen co keyword bat buoc, vi du:

```text
Khong hoan tien truc tiep neu giao dich co dau hieu gian lan va phai chuyen case cho Reviewer hoac Fraud team.
```

3. Chay evaluation run truoc khi approve correction neu muon lay baseline.
4. Approve correction.
5. Chay evaluation run lai.

Ky vong:

- Run truoc co the fail neu answer thieu keyword.
- Run sau pass khi answer co keyword expected.
- Dashboard hien active eval cases va latest pass rate.

Bang chung "AI thong minh hon":

- Khong chi cam tinh "tra loi hay hon".
- Co before/after bang pass rate, passed cases, failed cases.
- Evaluation dung lai pipeline AI that.

## 13. Kich ban J - Dashboard va audit

Muc tieu: xem KPI va truy vet hanh dong quan trong.

Dashboard API yeu cau token Admin/Reviewer.

Vi du goi API:

```powershell
$login = Invoke-RestMethod `
  -Uri 'http://localhost:5000/api/auth/login' `
  -Method Post `
  -ContentType 'application/json' `
  -Body '{"email":"admin@example.local","password":"ChangeMe123!"}'

Invoke-RestMethod `
  -Uri 'http://localhost:5000/api/dashboard/summary' `
  -Headers @{ Authorization = "Bearer $($login.accessToken)" }
```

Ky vong dashboard:

- Document counts thay doi sau upload/approve.
- Wiki counts thay doi sau publish.
- AI question count tang sau hoi dap.
- Feedback incorrect count tang sau feedback sai.
- Evaluation pass rate hien neu da chay eval.
- Top cited sources hien nguon hay duoc AI dung.

Ky vong audit:

- Co log cho upload/approve/publish/correction/rebuild neu flow do co audit.
- Co actor va entity lien quan.

Bang chung "AI thong minh hon":

- Co KPI de theo doi chat luong va adoption.
- Co audit de kiem soat rui ro khi AI tac dong vao tri thuc.

## 14. Kich ban K - Knowledge index rebuild

Muc tieu: chung minh he thong co ledger de rebuild index khi doi embedding model/collection.

Buoc test:

1. Dang nhap Admin/Reviewer.
2. Mo trang Knowledge Index.
3. Xem summary: ledger count va keyword index count.
4. Chay rebuild khong reset truoc.
5. Neu can test sau hon, chay rebuild co reset collection.

Ky vong:

- Rebuild replay chunk tu SQLite ledger.
- Keyword index duoc tao lai.
- Khong can upload lai document goc.
- Sau rebuild, Q&A van tra loi duoc voi citation.

Bang chung "AI thong minh hon":

- Vector DB chi la index co the rebuild.
- SQLite ledger giu day du text/metadata chunk de debug va tai tao tri thuc.

## 15. Checklist pass/fail tong hop

Danh dau khi test:

```text
[ ] Build backend pass.
[ ] Backend tests pass.
[ ] Frontend build/test pass.
[ ] ChromaDB heartbeat pass.
[ ] Login 3 role thanh cong.
[ ] User upload document thanh cong.
[ ] Reviewer approve document thanh cong.
[ ] Document indexed va co metadata/section/summary.
[ ] AI tra loi cau co trong tai lieu kem citation.
[ ] AI noi thieu thong tin voi cau khong co nguon.
[ ] Keyword noi bo nhu Refund-SLA-4H tim dung source.
[ ] User khong thay noi dung folder khong co quyen.
[ ] Retrieval explain hien candidate/filter/score reasons.
[ ] Wiki draft co cau truc, missing information hoac related docs.
[ ] Published wiki duoc AI dung khi relevant.
[ ] Incorrect feedback tao quality issue.
[ ] Reviewer approve correction.
[ ] Cau hoi sau correction tra loi tot hon.
[ ] Evaluation before/after co pass rate.
[ ] Dashboard KPI cap nhat.
[ ] Audit log co hanh dong quan trong.
[ ] Knowledge index rebuild xong va Q&A van hoat dong.
```

## 16. Tieu chi ket luan "AI thong minh hon"

Co the ket luan ban hien tai thong minh hon MVP co ban neu dat it nhat cac diem sau:

- Retrieval khong chi dua vao vector: co keyword fallback, rerank va context packing.
- Cau tra loi co structured metadata: confidence, missing information, conflicts, follow-ups.
- Citation duoc validate theo context backend.
- Document ingestion tao metadata va section-aware chunks.
- Wiki generation tao draft co cau truc, khong copy thuan 900 ky tu dau.
- Feedback Incorrect tao vong cai thien co reviewer approve.
- Evaluation do duoc pass/fail truoc va sau correction.
- Retrieval explain giup reviewer nhin thay ly do AI chon nguon.

## 17. Loi thuong gap

### API dashboard tra 401

Endpoint dashboard yeu cau token Admin/Reviewer. Login truoc va gui header:

```text
Authorization: Bearer <accessToken>
```

### Q&A khong co citation

Kiem tra:

- Document da approve chua.
- Version da indexed chua.
- User co quyen folder chua.
- ChromaDB dang chay chua.
- Neu doi embedding model/dimension, da dung collection moi hoac rebuild index chua.

### AI tra loi mock, chua "thong minh"

Kiem tra config:

```text
AiProvider__Name=mock
```

`mock` dung cho local/test offline. Muon demo LLM that, doi sang provider `openai` hoac `openai-compatible`, set API key va collection phu hop embedding dimension.

### User thay thieu du lieu trong Q&A

Kiem tra permission folder/team/user. Neu permission dung ma van khong retrieve, dung Retrieval Explain de xem candidate bi loai vi scope, status, current version hay folder visibility.

## 18. Goi y bao cao ket qua test

Sau khi test, ghi lai bang ngan:

```text
Ngay test:
Nguoi test:
Moi truong:
Provider AI: mock/openai/openai-compatible
So document approved:
So wiki published:
So cau hoi AI:
So feedback incorrect:
Eval pass rate truoc correction:
Eval pass rate sau correction:

Nhan xet:
- Cau hoi AI tra loi tot:
- Cau hoi AI tra loi chua tot:
- Citation co kiem chung duoc khong:
- Permission co dung khong:
- De xuat bo sung tai lieu/correction:
```

