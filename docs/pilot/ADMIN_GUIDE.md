# Admin Guide

Tai lieu nay danh cho Admin/IT support van hanh pilot Internal Knowledge Copilot.

## 1. Vai tro Admin

Admin co the:

- Tao va cap nhat user.
- Gan role Admin, Reviewer, User.
- Quan ly team.
- Quan ly folder.
- Gan quyen folder cho team/user.
- Xem dashboard.
- Xem audit log.
- Ho tro loi dang nhap va phan quyen.

## 2. Tao user

Khi tao user:

- Dung email cong ty neu co.
- Gan dung role.
- Gan team chinh.
- Thong bao user ve duong dan he thong va cach dang nhap.

Role goi y:

- Admin: chi cap cho nguoi van hanh he thong.
- Reviewer: cap cho nguoi duoc team chi dinh kiem soat noi dung.
- User: cap cho nguoi dung pilot thong thuong.

## 3. Quan ly team

Moi team pilot nen co:

- Ten team ro rang.
- It nhat 1 reviewer.
- Danh sach user tham gia pilot.

Khong nen tao qua nhieu team neu pilot con nho. Cau truc team can don gian de de phan quyen va ho tro.

## 4. Quan ly folder

Folder nen phan theo cach team lam viec, vi du:

```text
/Support
/Support/FAQ
/Support/Refund
/Engineering
/Operations
/Onboarding
```

Nguyen tac:

- Ten folder ngan, ro nghia.
- Khong tao folder qua sau neu chua can.
- Moi folder nen co owner/reviewer.
- Phan quyen folder truoc khi user upload tai lieu.

## 5. Phan quyen

Truoc khi cap quyen, can xac dinh:

- Team nao duoc xem folder nao.
- Ai la reviewer cua folder do.
- Co can user-specific permission khong.

Nguyen tac:

- Cap quyen toi thieu can thiet.
- Khong cap company-wide neu tai lieu chi danh cho mot team.
- Khi user bao khong thay tai lieu, kiem tra folder permission truoc.
- Khi user chuyen team, cap nhat lai quyen.

## 6. Dashboard

Admin nen dung dashboard de theo doi:

- So tai lieu trong he thong.
- So tai lieu pending/approved/rejected.
- So cau hoi AI.
- So feedback Incorrect.
- So wiki published.
- Top cited sources.

Dashboard dung de danh gia pilot, khong dung de danh gia ca nhan user.

## 7. Audit log

Audit log dung de truy vet cac hanh dong quan trong:

- Quan ly user/team.
- Thay doi folder/permission.
- Approve/reject document.
- Publish/reject wiki.
- Xu ly feedback.

Khi co nghi ngo ve quyen hoac thay doi noi dung, xem audit log truoc khi ket luan.

## 8. Xu ly su co thuong gap

User khong dang nhap duoc:

- Kiem tra user co ton tai khong.
- Kiem tra role/team.
- Neu can, dat lai mat khau theo quy trinh noi bo.

User khong thay tai lieu:

- Kiem tra folder permission.
- Kiem tra tai lieu da approved chua.
- Kiem tra tai lieu co nam dung folder khong.

AI khong tra loi duoc:

- Kiem tra tai lieu da approve va indexed chua.
- Kiem tra user co quyen voi folder/document khong.
- Kiem tra ChromaDB/API dang chay neu la moi truong local.

Reviewer khong thay queue:

- Kiem tra user co role Reviewer khong.
- Kiem tra co feedback/document pending khong.

## 9. Sau pilot

Admin can ho tro tong hop:

- Danh sach user tham gia.
- KPI tu dashboard.
- Su co phan quyen.
- Su co dang nhap.
- De xuat cai tien van hanh neu mo rong.

