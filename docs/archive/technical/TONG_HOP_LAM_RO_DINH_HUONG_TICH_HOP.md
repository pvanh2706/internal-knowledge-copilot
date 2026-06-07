# Tong hop lam ro dinh huong tich hop he sinh thai phan mem

> Archived: huong dan hien hanh nam o `docs/technical/HUONG_DAN_TICH_HOP_BEN_THU_3.md`.

Ngay lap: 2026-06-03

Tai lieu nay tong hop cac cau tra loi lam ro ve dinh huong phat trien Internal Knowledge Copilot thanh nen tang AI co the tich hop voi he sinh thai phan mem cua cong ty nhu CRM, phan mem ban hang va cac san pham khac.

## 1. Muc tieu tong quan

Internal Knowledge Copilot khong chi la mot ung dung upload tai lieu va hoi dap noi bo. Huong phat trien tiep theo la tro thanh mot nen tang AI dung chung, co kha nang:

- Tich hop vao nhieu san pham nghiep vu cua cong ty.
- Hoi dap va goi y dua tren tai lieu, quy trinh va du lieu ngu canh cua tung phan mem.
- Giam sat quy trinh va goi y buoc tiep theo cho nguoi dung.
- Thuc hien hanh dong nghiep vu sau khi duoc nguoi dung hoac rule cho phep.
- Ho tro nhieu khach hang tren cung mot he thong multi-tenant.

## 2. Ket qua lam ro

### Cau 1. Mo hinh trien khai

Quyet dinh:

- Uu tien xay dung mot he thong multi-tenant dung chung cho nhieu khach hang.

Ham y thiet ke:

- Moi bang nghiep vu quan trong can co ranh gioi tenant.
- Retrieval, indexing, audit, feedback va cau hinh AI khong duoc tron du lieu giua cac tenant.
- Can co chinh sach dat ten collection/index/vector namespace theo tenant.
- Admin noi bo va tenant admin can duoc tach vai tro ro rang.

### Cau 2. Loai phan mem se tich hop

Quyet dinh:

- Truoc mat tich hop voi cac phan mem ma cong ty kiem soat source code.
- Sau do co the mo rong de ho tro phan mem ben thu ba hoac phan mem rieng cua khach hang.

Ham y thiet ke:

- Giai doan dau co the thiet ke contract noi bo sau va don gian hon.
- Van can tao boundary cho connector/integration de sau nay khong phai sua loi AI core.
- Khong nen hard-code logic CRM hoac ban hang vao module RAG/tai lieu.

### Cau 3. Pham vi hanh dong cua AI

Quyet dinh:

- AI khong chi goi y hoac danh gia.
- AI duoc phep tu dong tao task, cap nhat deal, doi trang thai va gui thong bao sau khi duoc nguoi dung hoac rule duyet.

Ham y thiet ke:

- Can tach lop "recommendation" va lop "action execution".
- Moi action can co policy, approval, audit va trang thai thuc thi.
- Action gui sang he thong goc phai idempotent de tranh thuc hien lap.
- He thong goc van nen la noi thuc thi action cuoi cung va kiem tra quyen lan cuoi.

### Cau 4. Cach xu ly quyen va kho tri thuc co nhieu tai lieu

Quyet dinh:

- Chon mo hinh hybrid.
- Copilot sync metadata, noi dung da extract/chunk, vector index, version va ACL snapshot co ban.
- Khi co hanh dong nhay cam hoac truoc khi hien thi/thuc thi noi dung quan trong, Copilot revalidate quyen voi he thong goc.
- He thong goc van la source of truth ve quyen that va action that.

Ly do:

- CRM co the co rat nhieu tai lieu, neu moi lan hoi AI moi truy van toan bo he thong goc thi cham va kho retrieval tot.
- Sync index giup AI tim kiem nhanh, ranking tot va co the xu ly RAG on dinh.
- Revalidate quyen giup giam rui ro leak du lieu khi quyen thay doi hoac khi action co tac dong nghiep vu.

Luong xu ly de xuat:

```text
CRM / File System / Business App
-> event hoac scheduled sync
-> Copilot luu metadata + extracted text + chunks + embeddings + ACL snapshot
-> user hoi hoac he thong gui event nghiep vu
-> Copilot retrieval nhanh bang index rieng theo tenant/application
-> revalidate quyen/action voi he thong goc khi can
-> tra goi y hoac gui action request sang he thong goc
```

### Cau 5. Yeu cau on-premise, data residency va AI provider noi bo

Quyet dinh:

- Hien tai chua can ho tro ngay.
- Can note lai de nghien cuu va phat trien cho cac goi enterprise sau nay.

Ham y thiet ke:

- Khong can day vao scope gan nhat cac yeu cau on-premise phuc tap.
- Tuy nhien khong nen thiet ke khoa chat vao mot AI provider duy nhat.
- Cau hinh AI provider nen co kha nang tien hoa thanh cau hinh theo tenant.
- Khong nen de du lieu/prompt/log phu thuoc vao mot mo hinh luu tru kho tach tenant.

### Cau 6. Bai toan danh gia won/lost trong CRM

Quyet dinh:

- Giai doan dau chua can scoring bang model hoc tu du lieu lich su deal.
- AI se dua tren quy trinh, trang thai deal, hoat dong gan day, ghi chu, task, email/call log de dua ra nhan dinh va goi y.

Ham y thiet ke:

- Day la bai toan reasoning theo ngu canh va quy trinh, khong phai predictive ML day du o giai doan dau.
- Can uu tien thu thap context deal tot, mapping quy trinh voi tung stage va sinh goi y co giai thich.
- Nen luu feedback cua nguoi dung ve goi y AI de sau nay xay bo du lieu danh gia/chat luong.

## 3. Cac quyet dinh da chot

- Huong san pham: multi-tenant SaaS/platform dung chung cho nhieu khach hang.
- Huong tich hop: truoc mat tich hop sau voi phan mem do cong ty kiem soat source code.
- Huong mo rong: chuan bi connector boundary de sau nay ho tro ben thu ba.
- Huong quyen va tri thuc: hybrid sync index/ACL snapshot, revalidate quyen/action voi he thong goc khi can.
- Huong AI action: AI co the de xuat va thuc thi action thong qua co che approval/policy.
- Huong CRM won/lost: giai doan dau dung LLM reasoning + quy trinh + activity context, chua can predictive ML.
- Huong enterprise: on-premise/data residency/local provider ghi nhan de nghien cuu, chua phai scope gan nhat.

## 4. Cac gia dinh lam viec

- He thong CRM/ban hang co the bo sung API/webhook phuc vu Copilot.
- Copilot co the luu ban sao index cua tai lieu, metadata va ACL snapshot.
- He thong goc chap nhan co API revalidate quyen va API thuc thi action.
- Nguoi dung hoac rule co the phe duyet action truoc khi action duoc gui sang he thong goc.
- Du lieu lich su CRM hien chua duoc xem la nguon chinh de tinh won/lost score.

## 5. Cac diem can nghien cuu tiep

- Muc co lap tenant: chung database co tenant_id, schema rieng, hay database rieng cho tung tenant.
- Cau truc tenant admin, internal admin va support access.
- Chuan connector cho phan mem ben thu ba.
- Co che ma hoa secret/API key theo tenant.
- Chinh sach luu prompt, log, context va PII theo tenant.
- Kich thuoc tai lieu CRM du kien va chien luoc incremental indexing.
- Cach dinh nghia rule approval cho AI action trong tung ung dung nghiep vu.
