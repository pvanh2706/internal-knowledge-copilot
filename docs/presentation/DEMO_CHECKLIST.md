# Demo checklist

Dung checklist nay truoc khi gap sep hoac demo cho team pilot.

## 1. Noi dung can chuan bi

- 1 bo slide theo `SLIDE_DECK_OUTLINE.md`.
- 1 tai lieu mau de upload.
- 1 cau hoi mau co the tra loi duoc tu tai lieu.
- 1 cau hoi mau khong du thong tin de AI hoi lai/phan hoi thieu context.
- 1 feedback Incorrect mau.
- 1 wiki draft/publish flow mau.

## 2. Tai khoan demo

- Admin: de quan ly user/team/folder va xem audit.
- Reviewer: de approve tai lieu, xu ly feedback, publish wiki.
- User: de upload tai lieu, hoi AI, gui feedback.

Neu dung seed local, xem thong tin tai khoan trong `DEVELOPMENT.md`.

## 3. Kiem tra truoc demo

- Backend chay duoc.
- Frontend chay duoc.
- ChromaDB chay duoc.
- Dang nhap duoc 3 role Admin/Reviewer/User.
- Upload tai lieu thanh cong.
- Reviewer approve tai lieu thanh cong.
- AI Q&A tra loi co citation.
- Feedback Incorrect vao reviewer queue.
- Generate va publish wiki thanh cong.
- Dashboard hien KPI.

## 4. Lenh kiem tra nhanh

Backend:

```powershell
dotnet test src/backend/InternalKnowledgeCopilot.sln
```

Frontend:

```powershell
cd src/frontend
npm run build
npm test
```

Smoke:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-mvp.ps1
```

## 5. Nguyen tac khi demo

- Noi theo van de va gia tri, khong noi theo code.
- Demo luong nghiep vu that cua User/Reviewer/Admin.
- Moi cau tra loi AI can chi vao citation.
- Khi noi ve AI sai, chu dong noi co feedback va reviewer queue.
- Khi noi ve bao mat, noi ve phan quyen va viec tai lieu chua approve khong duoc dung cho AI.
- Ket thuc bang de xuat pilot nho, khong de xuat rollout toan cong ty ngay.

## 6. Cau hoi sep co the hoi

Hoi: AI co tra loi sai khong?

Tra loi goi y: Co the co, nen MVP khong de AI tra loi vo kiem soat. Cau tra loi co nguon, user co feedback dung/sai, reviewer co queue de xu ly, va tai lieu chua duyet khong duoc dung cho AI.

Hoi: Co lo tai lieu noi bo khong?

Tra loi goi y: Pilot de xuat chay noi bo, co phan quyen theo folder/team/user. Backend kiem tra quyen truoc khi dung nguon cho AI, va file upload khong nam trong public web root.

Hoi: Co nen trien khai toan cong ty ngay khong?

Tra loi goi y: Chua nen. Nen pilot voi 1-2 team, do KPI va feedback trong 2-4 tuan, sau do moi quyet dinh mo rong.

Hoi: Can gi de bat dau?

Tra loi goi y: Can chon team pilot, reviewer cua team, bo tai lieu noi bo dau tien, thoi gian pilot va KPI thanh cong.

