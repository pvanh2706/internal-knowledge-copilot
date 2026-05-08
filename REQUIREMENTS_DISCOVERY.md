# Internal Knowledge Copilot - Khảo sát yêu cầu

Ngày lập: 2026-05-08

## 1. Mục tiêu sản phẩm

Công ty có nhiều phòng ban như Kỹ thuật, Hỗ trợ khách hàng, Sản phẩm, Vận hành. Tài liệu hiện đang rải rác ở Confluence và file rời trên máy cá nhân. Nhân viên mất thời gian tìm tài liệu, hỏi lại người cũ, hoặc dùng tài liệu đã lỗi thời.

Ứng dụng mong muốn là một hệ thống nội bộ quản lý tri thức/tài liệu, có AI hỗ trợ hỏi đáp, kiểm chứng nguồn, feedback và về sau mở rộng thành Structured Wiki, hiểu codebase, API, workflow và business rule.

MVP tập trung trước cho 2 team:

- Kỹ thuật
- Hỗ trợ khách hàng

MVP là bản chứng minh giá trị nhưng không làm theo hướng demo bỏ đi. Thiết kế cần gọn, dễ chạy nội bộ, dễ đo hiệu quả và có đường mở rộng.

## 2. KPI thành công

KPI chính:

- Giảm thời gian tìm tài liệu.
- Tỷ lệ câu trả lời AI được feedback là đúng.

Chỉ số phụ có thể theo dõi:

- Số lượt hỏi đáp AI.
- Số feedback đúng/sai.
- Số tài liệu được upload.
- Số câu trả lời sai cần reviewer xử lý.
- Số tài liệu/wiki theo trạng thái.
- Tài liệu/wiki nào được AI sử dụng nhiều.

Cần tư vấn thêm cách đo chỉ số "giảm thời gian tìm tài liệu" sao cho ít làm phiền người dùng.

## 3. Người dùng và phạm vi ban đầu

Số lượng người dùng MVP:

- Khoảng 20-50 người trong 2 team.

Team sử dụng:

- Kỹ thuật và Hỗ trợ khách hàng dùng ngang nhau.
- Mỗi team có bộ tài liệu và quyền riêng.

Role MVP đề xuất đã được chấp nhận:

- Admin: quản lý user, team, quyền, thư mục.
- User: upload tài liệu, xem tài liệu/wiki có quyền, hỏi AI, feedback.
- Reviewer: duyệt/reject tài liệu, generate/duyệt/reject/publish wiki draft, xem feedback sai.

Tài khoản:

- Admin tạo tài khoản thủ công.
- Admin tạo mật khẩu ban đầu.
- User đổi mật khẩu sau lần đăng nhập đầu.
- Chưa cần chính sách mật khẩu phức tạp.

## 4. Phạm vi MVP đã chốt

MVP cần có:

- Upload tài liệu thủ công.
- Tổ chức tài liệu theo cây thư mục.
- Phân quyền theo team, thư mục và tài liệu.
- User thuộc team chính, nhưng có thể được cấp thêm quyền vào thư mục/tài liệu cụ thể.
- Tài liệu upload cần được Reviewer duyệt trước khi được sử dụng.
- Tài liệu chưa duyệt không được dùng cho AI Q&A hoặc sinh wiki.
- Tài liệu bị reject thì uploader xem lý do và upload lại.
- Versioning đơn giản cho tài liệu: lưu các phiên bản và đánh dấu bản hiện tại.
- AI Q&A trên tài liệu/wiki mà user có quyền, có khả năng hỏi theo toàn bộ phạm vi, theo thư mục hoặc theo tài liệu cụ thể.
- Câu trả lời AI luôn bằng tiếng Việt.
- Nếu tài liệu nguồn bằng tiếng Anh, AI được phép dịch/tóm tắt sang tiếng Việt.
- Câu trả lời AI có nguồn gồm tên tài liệu, đường dẫn/thư mục và đoạn trích ngắn liên quan.
- Nếu AI không đủ chắc, AI hỏi lại người dùng để làm rõ thay vì đoán.
- Feedback AI gồm đúng/sai và ghi chú.
- Feedback sai tạo danh sách cho Reviewer xử lý và làm dữ liệu nền để cải thiện AI/search sau này.
- Dashboard KPI đơn giản cho Admin và Reviewer.
- Audit log cơ bản cho upload, publish, sửa quyền và feedback.
- File upload lưu ở folder/file system nội bộ.
- User có quyền được xem và tải file gốc.
- MVP chưa cần preview file trong app, cho tải file là đủ.
- UI cần cân bằng 3 phần: quản trị tài liệu/wiki, hỏi đáp AI, review/duyệt.

## 5. Tài liệu và dữ liệu

Nguồn hiện tại:

- Confluence.
- File rời trên máy cá nhân.

Nhập liệu MVP:

- Ban đầu upload thủ công.
- Import Confluence để giai đoạn sau.

Định dạng file MVP:

- PDF.
- DOCX.
- Markdown.
- TXT.

Định dạng để sau:

- HTML.
- Excel.
- PowerPoint.
- Ảnh/screenshot.

Ngôn ngữ:

- Chủ yếu tiếng Việt.
- Thỉnh thoảng có tiếng Anh.

Quy mô ban đầu:

- Khoảng 100-500 file.
- Mỗi file tối đa 20MB.
- Mỗi tài liệu dưới 70 trang.

Tần suất thay đổi:

- Đa số ít thay đổi.
- Tài liệu thay đổi khi team Kỹ thuật update version mới.

## 6. Workflow tài liệu

Upload:

- Bất kỳ User nào cũng có thể upload.
- Tài liệu sau upload ở trạng thái chờ duyệt.
- Tài liệu chưa duyệt không được dùng cho AI Q&A hoặc sinh wiki.

Duyệt:

- Reviewer duyệt hoặc reject tài liệu.
- Nếu reject, Reviewer nhập lý do.
- Uploader xem lý do và upload lại.

Versioning:

- Cần versioning đơn giản.
- Cần tư vấn cách xác định "cùng một tài liệu" khi upload phiên bản mới.
- Cần tư vấn xử lý version cũ khi version mới đang chờ duyệt.

Xóa tài liệu/thư mục:

- Chưa chốt.
- Cần tư vấn, khả năng nên dùng soft delete trong MVP.

## 7. Structured Wiki

Mục tiêu ưu tiên số một của MVP là chuẩn hóa tri thức thành wiki.

Hướng đã chốt:

- AI tự sinh draft wiki từ tài liệu.
- Reviewer duyệt/chỉnh theo quy trình và publish.
- MVP chỉ cần duyệt và publish, chưa cần editor chỉnh sửa nội dung trong app.
- Nếu draft chưa đạt, Reviewer reject và nhập lý do.
- MVP dùng nút thủ công "Generate wiki draft" để kiểm soát chi phí.
- Về sau có thể hỗ trợ tự động sinh wiki sau khi xử lý tài liệu.
- Wiki publish theo ngôn ngữ của tài liệu nguồn.
- MVP chưa cần versioning wiki.
- MVP tạm thời chưa cần browse/search wiki riêng; wiki publish chủ yếu để AI sử dụng.

Cần tư vấn:

- Sinh wiki theo từng tài liệu hay theo thư mục/chủ đề.
- Wiki sau publish nên là nguồn đọc chính, nguồn ưu tiên cho AI Q&A, hay lớp tri thức chuẩn hóa cho Reviewer.
- Khi có cả wiki publish và tài liệu gốc, AI nên ưu tiên nguồn nào.
- Khi tài liệu bị xóa/ẩn, wiki liên quan nên xử lý thế nào.

Quyền wiki:

- Wiki publish có thể public toàn công ty hoặc public theo thư mục/quyền của thư mục nguồn.
- Khi publish wiki toàn công ty, Reviewer cần xác nhận nội dung được phép public nội bộ.
- Cần tư vấn thêm để tránh rò rỉ thông tin khi wiki được publish từ tài liệu gốc có quyền hạn chế.

## 8. AI Q&A

Phạm vi hỏi:

- Toàn bộ tài liệu/wiki user có quyền.
- Theo thư mục được chọn.
- Theo tài liệu cụ thể.

Nguồn trả lời:

- Tên tài liệu.
- Đường dẫn/thư mục tài liệu.
- Đoạn trích ngắn liên quan.

Hành vi khi không đủ thông tin:

- AI hỏi lại người dùng để làm rõ.

Ngôn ngữ:

- Luôn trả lời bằng tiếng Việt.
- Nếu nguồn tiếng Anh, AI được phép dịch/tóm tắt sang tiếng Việt.

Lịch sử hỏi đáp:

- MVP chưa cần chức năng xem lại lịch sử hỏi đáp.
- Tuy nhiên để đo feedback đúng/sai, hệ thống vẫn cần lưu tối thiểu bản ghi interaction đã được feedback.

Feedback:

- User đánh dấu đúng/sai.
- Có ghi chú.
- Feedback sai tạo danh sách cho Reviewer xử lý.
- Danh sách feedback sai tối thiểu gồm câu hỏi và câu trả lời AI.
- Nên cân nhắc lưu thêm nguồn trích dẫn, ghi chú feedback, thời gian và trạng thái xử lý để Reviewer làm việc hiệu quả.

## 9. Tìm kiếm

Chưa chốt có cần search thông thường trong MVP hay không.

Cần tư vấn:

- Có nên có keyword search cơ bản để phục vụ KPI giảm thời gian tìm tài liệu.
- Có nên tận dụng Elasticsearch hiện có ngay từ MVP hay giữ app đơn giản trước.

## 10. Dashboard KPI

Cần dashboard đơn giản cho:

- Admin.
- Reviewer.

Phạm vi xem mong muốn:

- Toàn hệ thống.
- Theo team.
- Theo thư mục/tài liệu.
- Theo khoảng thời gian.

Làm đơn giản trong MVP.

Không cần export CSV/Excel trong MVP.

Cần tư vấn:

- Cách đo "giảm thời gian tìm tài liệu".
- Có cần ẩn danh người dùng trong analytics hay không.
- Admin và Reviewer nên xem dữ liệu cá nhân hay chỉ tổng quan.

## 11. Bảo mật và quyền riêng tư

Loại tài liệu MVP:

- Đa số là tài liệu nội bộ bình thường, không quá nhạy cảm.
- Hợp đồng/tài chính để giai đoạn sau.

AI cloud:

- Có thể dùng cloud AI nếu có cam kết enterprise/privacy rõ ràng.
- Có thể dùng thêm API riêng nếu cần.
- ChatGPT Plus hiện có không được xem là API/enterprise setup cho ứng dụng nội bộ.

Masking/redaction:

- MVP chưa cần masking/redaction vì tài liệu ít nhạy cảm.
- Sau này muốn có chức năng chặn upload nếu phát hiện credential/secrets.

Audit log:

- Cần audit log cơ bản cho upload, publish, sửa quyền và feedback.
- Chưa cần audit log đầy đủ cho xem tài liệu và hỏi AI.

Virus/malware scan:

- MVP chưa cần.
- Cần tư vấn cho giai đoạn sau.

Download:

- User có quyền được tải file gốc.

## 12. Công nghệ và hạ tầng hiện có

Stack hiện tại của team:

- Backend: .NET, biết sơ qua Python.
- Frontend: Vue.
- Database: SQL Server.
- Deploy: IIS trên Windows Server.

Ưu tiên MVP:

- Dùng stack thuận lợi hơn cho AI/RAG nếu cần.
- Tuy nhiên tạm thời deploy qua IIS trên Windows Server.

Dịch vụ có sẵn:

- SQL Server.
- Elasticsearch.
- Redis.

Lưu trữ file:

- Folder/file system nội bộ.

Log:

- Log kỹ thuật đơn giản lưu bằng file log trên server.
- Audit log nghiệp vụ có thể cần lưu database để phục vụ dashboard/truy vết.

Notification:

- Tạm thời bỏ email.
- Không dùng SendGrid trong MVP.
- Chỉ hiển thị trạng thái/thông báo trên giao diện; user tự refresh/load lại để kiểm tra.

## 13. Ngoài phạm vi MVP

Những mục đã chốt chưa làm trong MVP:

- Import Confluence tự động.
- Hiểu codebase/API/workflow/business rule.
- Xử lý Excel/PowerPoint/HTML/ảnh.
- SSO.
- Masking/redaction nâng cao.
- Audit log đầy đủ cho xem tài liệu/hỏi AI.
- Fine-tuning model.
- Mobile app.
- Preview file trong app.
- Browse/search wiki riêng.
- Versioning wiki.
- Export dashboard/report CSV/Excel.
- Email notification.
- Virus/malware scanning.
- Giới hạn dung lượng theo team/user.

## 14. Các điểm cần tư vấn tiếp theo

### 14.1 Phạm vi MVP nên cắt như thế nào

Cần đề xuất lát cắt MVP nhỏ nhất nhưng có giá trị:

- Tài liệu nguồn.
- Duyệt tài liệu.
- AI Q&A có nguồn.
- Feedback đúng/sai.
- AI sinh wiki draft.
- Reviewer publish/reject.
- Dashboard KPI đơn giản.

### 14.2 Đơn vị sinh wiki

Cần so sánh:

- Sinh wiki từ từng tài liệu.
- Sinh wiki từ thư mục/chủ đề gom nhiều tài liệu.
- MVP có nên bắt đầu từ từng tài liệu rồi mở rộng sang thư mục/chủ đề hay không.

### 14.3 Vai trò của wiki trong AI Q&A

Cần quyết định:

- AI ưu tiên wiki publish trước hay tài liệu gốc trước.
- Wiki publish có phải là nguồn tri thức chuẩn hóa cho AI hay chỉ là bổ sung.
- Cách tránh việc wiki public làm lộ nội dung từ tài liệu hạn chế quyền.

### 14.4 Phân quyền và hiển thị cây thư mục

Cần tư vấn:

- User không có quyền có thấy tên thư mục không.
- Nên ẩn hoàn toàn thư mục/tài liệu không có quyền hay hiển thị partial.
- Quyền theo team, thư mục, tài liệu nên triển khai tối giản ra sao để không over-engineering.

### 14.5 Versioning tài liệu

Cần tư vấn:

- Cách user upload version mới ít lỗi nhất.
- Version cũ có nên tiếp tục là bản hiện tại cho đến khi version mới được duyệt.
- Cách gắn wiki/embedding/AI index với document version.

### 14.6 Search thông thường

Cần tư vấn:

- Có nên làm keyword search cơ bản trong MVP.
- Có nên dùng Elasticsearch ngay hay để sau.
- Nếu dùng Elasticsearch, phạm vi nào là tối thiểu.

### 14.7 Đo KPI "giảm thời gian tìm tài liệu"

Cần tư vấn cách đo:

- Đo từ lúc user search/hỏi đến lúc click/mở/tải nguồn.
- Khảo sát nhanh trước/sau.
- Nút "bắt đầu tìm" và "đã tìm thấy".
- Kết hợp log hành vi và feedback nhẹ.

### 14.8 Analytics và quyền riêng tư

Cần tư vấn:

- Admin/Reviewer có xem được theo user cụ thể không.
- Dashboard nên hiển thị tổng quan hay có drill-down.
- Có cần ẩn danh dữ liệu người dùng trong dashboard không.

### 14.9 Stack công nghệ và kiến trúc

Chưa tư vấn trong tài liệu này theo đúng quy trình. Sau khi bạn xác nhận bản tổng hợp này, bước tiếp theo mới nên đề xuất:

- Stack công nghệ.
- Kiến trúc MVP.
- Luồng xử lý tài liệu.
- RAG/search/indexing.
- Cách deploy trên Windows Server/IIS.
- Cách mở rộng về sau.
