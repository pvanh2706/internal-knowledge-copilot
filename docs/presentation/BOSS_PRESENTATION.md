# Internal Knowledge Copilot - Noi dung trinh bay voi sep

Muc tieu cua tai lieu nay la giup trinh bay du an theo goc nhin van hanh va gia tri kinh doanh, khong di vao code.

## 1. Thong diep chinh

Internal Knowledge Copilot la mot MVP giup cong ty tap trung tri thuc noi bo, cho nhan vien hoi dap bang tieng Viet tren tai lieu da duyet, co nguon trich dan ro rang va co reviewer kiem soat chat luong noi dung.

De xuat hien tai khong phai trien khai toan cong ty ngay, ma la chay pilot co kiem soat voi mot vai team trong 2-4 tuan de do hieu qua thuc te.

## 2. Van de hien tai

- Tai lieu nam rai rac trong nhieu folder, file, kenh trao doi va nguoi nam giu.
- Nhan su moi mat nhieu thoi gian de tim dung quy trinh, tai lieu, cau tra loi.
- Cac team thuong hoi lap lai nhung cau giong nhau.
- Tai lieu cu, trung lap hoac khong ro ban nao la ban dung de tham chieu.
- Neu dung AI truc tiep ma khong co nguon va reviewer, rui ro tra loi sai hoac dung tai lieu chua duyet.

## 3. Muc tieu thu nghiem

- Tap trung tai lieu noi bo quan trong vao mot he thong co phan quyen.
- Cho nhan vien hoi dap bang tieng Viet tren pham vi tai lieu duoc phep truy cap.
- Cau tra loi AI phai co nguon trich dan de nguoi dung kiem chung.
- Chi tai lieu da duoc reviewer approve moi duoc dua vao AI Q&A.
- Bien tai lieu goc thanh wiki noi bo da duoc reviewer publish.
- Co dashboard va feedback de do chat luong cau tra loi trong qua trinh pilot.

## 4. Giai phap da co trong MVP

- Dang nhap, quan ly user, role va team.
- Quan ly folder va phan quyen theo team/user.
- Upload tai lieu PDF, DOCX, Markdown va TXT.
- Reviewer approve/reject tai lieu truoc khi dua vao tri thuc AI.
- Versioning tai lieu, ban moi chua duyet khong thay the ban hien tai.
- AI Q&A co nguon trich dan.
- Feedback dung/sai cho cau tra loi AI.
- Reviewer queue de xu ly feedback sai.
- Sinh wiki draft tu tai lieu da duyet.
- Reviewer publish wiki de tao lop tri thuc chuan hoa.
- Dashboard KPI va audit log cho Admin/Reviewer.

## 5. Gia tri ky vong cho cong ty

- Giam thoi gian tim kiem tai lieu va cau tra loi noi bo.
- Giam viec hoi dap lap lai giua cac team.
- Tang toc onboarding cho nhan su moi.
- Chuan hoa tri thuc tu tai lieu goc sang wiki noi bo.
- Kiem soat tot hon viec AI dung tai lieu nao de tra loi.
- Giu duoc quyen truy cap theo team/folder.
- Co so lieu de danh gia: so cau hoi, ty le feedback sai, nguon duoc dung nhieu, tai lieu can chuan hoa.

## 6. Luong demo nen trinh bay

Demo theo nghiep vu, khong demo code:

1. Admin tao user/team va folder.
2. Admin/Reviewer gan quyen folder cho team.
3. User upload mot tai lieu noi bo.
4. Reviewer approve tai lieu.
5. He thong xu ly va index tai lieu.
6. User hoi AI mot cau lien quan den tai lieu.
7. AI tra loi bang tieng Viet kem nguon trich dan.
8. User danh dau cau tra loi la dung hoac sai.
9. Reviewer xem feedback sai trong queue.
10. Reviewer generate wiki draft tu tai lieu da duyet.
11. Reviewer publish wiki.
12. User hoi lai va AI uu tien dung wiki published khi phu hop.
13. Admin/Reviewer xem dashboard KPI va audit log.

## 7. Vai tro trong he thong

Admin:

- Quan ly user, team, folder, quyen truy cap.
- Xem audit log.
- Xem dashboard.

Reviewer:

- Duyet/reject tai lieu.
- Xu ly feedback sai.
- Tao va publish wiki.
- Xem dashboard.

User:

- Upload tai lieu.
- Hoi dap AI trong pham vi duoc cap quyen.
- Gui feedback dung/sai cho cau tra loi.

## 8. De xuat pilot

Pham vi de xuat:

- Chon 1-2 team dau tien, vi du Support, Ky thuat hoac Van hanh.
- Moi team chon 1 reviewer phu trach chat luong noi dung.
- Dua vao 30-50 tai lieu noi bo quan trong.
- Chay thu 2-4 tuan.
- Moi tuan review KPI va feedback mot lan.

Chi so can do:

- So cau hoi AI moi ngay/moi tuan.
- Ty le cau tra loi bi danh dau sai.
- So feedback sai da duoc reviewer xu ly.
- So tai lieu duoc approve.
- So wiki duoc publish.
- Top nguon duoc trich dan nhieu.
- Thoi gian uoc tinh tiet kiem khi tim cau tra loi.

## 9. Rui ro va cach kiem soat

AI tra loi sai:

- Cau tra loi co citation de kiem chung.
- User co the feedback dung/sai.
- Reviewer xu ly cau tra loi sai trong queue.

Lo thong tin noi bo:

- Co phan quyen folder/team/user.
- Backend recheck quyen truoc khi dung nguon cho AI.
- File upload khong nam trong public web root.

Tai lieu chua duyet bi dua vao AI:

- Chi document approved/current va wiki published moi duoc index va dung cho Q&A.

Chat luong tai lieu goc kem:

- Reviewer approve/reject tai lieu.
- Wiki published tao lop tri thuc chuan hoa tu tai lieu goc.

MVP chua phai he thong production lon:

- Chay pilot gioi han truoc.
- Do KPI va feedback truoc khi mo rong.
- Chi mo rong sau khi co ket qua pilot ro rang.

## 10. Quyet dinh can xin sep

Can sep phe duyet:

- Cho phep chay pilot voi 1-2 team.
- Chi dinh reviewer cua moi team.
- Cho phep chon bo tai lieu noi bo de thu nghiem.
- Thong nhat thoi gian pilot 2-4 tuan.
- Thong nhat tieu chi danh gia thanh cong.

## 11. Cau chot de trinh bay

Em khong de xuat trien khai toan cong ty ngay. Em de xuat chay pilot co kiem soat voi mot vai team, dung tai lieu noi bo that, do KPI va feedback thuc te. Neu ket qua tot, minh moi tinh den viec mo rong va hardening cho production.

