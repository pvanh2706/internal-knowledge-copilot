# Hướng Dẫn Đào Tạo Team Triển Khai Internal Knowledge Copilot

Ngày cập nhật: 2026-06-07

Tài liệu này dành cho team triển khai, tư vấn và hướng dẫn khách hàng sử dụng Internal Knowledge Copilot. Mục tiêu là giúp team hiểu chức năng hệ thống ở mức đủ để:

- Tư vấn đúng phạm vi sản phẩm.
- Hướng dẫn khách hàng chuẩn bị dữ liệu và vận hành pilot.
- Demo các luồng chính một cách nhất quán.
- Giải thích vì sao hệ thống cần reviewer, phân quyền, citation và feedback.
- Nhận diện tình huống cần chuyển cho Admin/IT hoặc team kỹ thuật.

Đây không phải tài liệu code. Nếu cần chi tiết kỹ thuật, đọc thêm [Technical system overview](../technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md).

## 1. Cách Định Vị Sản Phẩm

Internal Knowledge Copilot là hệ thống quản lý tri thức nội bộ có AI hỗ trợ. Hệ thống giúp doanh nghiệp đưa tài liệu nội bộ vào một quy trình có kiểm soát, sau đó cho nhân viên hỏi đáp bằng AI trên các tài liệu đã được duyệt và có nguồn trích dẫn.

Thông điệp ngắn khi tư vấn:

```text
Internal Knowledge Copilot giúp nhân viên tìm câu trả lời từ tài liệu nội bộ nhanh hơn, có nguồn kiểm chứng, có phân quyền, có reviewer kiểm soát chất lượng, và có feedback loop để cải thiện tri thức theo thời gian.
```

Điểm khác với chatbot thông thường:

- Không trả lời tự do ngoài dữ liệu nội bộ đã được duyệt.
- Mỗi câu trả lời cần có citation để người dùng kiểm chứng.
- Tài liệu phải qua reviewer trước khi AI được dùng.
- Phân quyền folder/user/team được enforce trước khi đưa nguồn vào câu trả lời.
- Feedback sai được đưa vào queue để reviewer xử lý.
- Wiki giúp chuẩn hóa tri thức từ tài liệu gốc dài hoặc rời rạc.

Điểm không nên hứa:

- Không hứa AI luôn đúng.
- Không hứa thay thế hoàn toàn quy trình nội bộ hiện tại.
- Không hứa tự động hiểu mọi file scan/ảnh nếu chưa có OCR.
- Không hứa dùng được tài liệu chưa được duyệt.
- Không hứa bỏ qua phân quyền để tìm đủ câu trả lời.

## 2. Đối Tượng Sử Dụng Chính

| Vai trò | Mục tiêu khi dùng hệ thống | Team triển khai cần hướng dẫn |
| --- | --- | --- |
| User | Hỏi AI, xem citation, upload tài liệu, gửi feedback | Cách hỏi đúng, cách đọc nguồn, cách báo sai |
| Reviewer | Duyệt tài liệu, publish wiki, xử lý feedback | Tiêu chí approve/reject, kiểm tra wiki, xử lý lỗi AI |
| Admin/IT | Quản lý user, role, team, folder, quyền, vận hành | Setup quyền, hỗ trợ đăng nhập, audit, backup, escalation |
| Pilot owner | Điều phối pilot và đo hiệu quả | Chọn phạm vi, KPI, lịch review, báo cáo kết quả |
| Team triển khai | Tư vấn, đào tạo, demo, hỗ trợ khách hàng | Hiểu luồng nghiệp vụ, rủi ro, giới hạn, checklist triển khai |

## 3. Khái Niệm Cần Nắm

Document:

- Tài liệu logic người dùng upload.
- Có folder, title, description, status và current version.

Document version:

- Một phiên bản file cụ thể của document.
- Version mới chưa duyệt không thay thế version hiện hành.
- AI chỉ dùng version đã được approve, xử lý xong và là current version.

Folder:

- Nơi tổ chức tài liệu.
- Là đơn vị phân quyền chính.
- User chỉ nhìn thấy và hỏi AI trên folder họ có quyền.

Reviewer:

- Người kiểm soát chất lượng tri thức.
- Reviewer quyết định tài liệu nào được approve, wiki nào được publish, feedback sai được xử lý thế nào.

Vector store:

- Nơi lưu embedding để tìm đoạn nội dung liên quan bằng semantic search.
- Runtime hiện tại dùng ChromaDB.
- Vector store không phải source of truth về quyền.

Citation:

- Nguồn trích dẫn đi kèm câu trả lời AI.
- Giúp người dùng kiểm tra câu trả lời dựa trên tài liệu nào.

Wiki:

- Lớp tri thức chuẩn hóa do AI sinh draft và reviewer publish.
- Wiki giúp câu trả lời ổn định hơn khi tài liệu gốc dài, rối hoặc có nhiều cách diễn đạt.

Feedback:

- User đánh dấu câu trả lời đúng/sai.
- Feedback sai tạo dữ liệu cho reviewer cải thiện tài liệu, wiki hoặc tri thức bổ sung.

## 4. Bản Đồ Chức Năng

| Nhóm chức năng | Mục đích | Ai dùng chính |
| --- | --- | --- |
| Đăng nhập và đổi mật khẩu | Bảo vệ truy cập hệ thống | Tất cả user |
| Quản lý user/team/role | Chuẩn bị tổ chức và quyền | Admin/IT |
| Quản lý folder và quyền | Chia phạm vi tri thức theo team | Admin/IT |
| Upload tài liệu | Đưa tài liệu vào hệ thống | User, Reviewer |
| Duyệt tài liệu | Chặn dữ liệu sai/nhạy cảm trước khi AI dùng | Reviewer |
| Xử lý/index tài liệu | Biến file thành chunks có thể truy vấn | Hệ thống |
| Hỏi AI | Tìm câu trả lời từ tài liệu/wiki được phép | User |
| Citation | Kiểm chứng nguồn câu trả lời | User, Reviewer |
| Feedback | Báo đúng/sai để cải thiện chất lượng | User, Reviewer |
| Generate wiki draft | Tạo bản nháp tri thức chuẩn hóa | Reviewer |
| Publish wiki | Đưa wiki thành nguồn tri thức chính thức | Reviewer |
| Dashboard/KPI | Theo dõi adoption và chất lượng | Admin, Reviewer, Pilot owner |
| Audit log | Truy vết hành động quan trọng | Admin/IT |
| Integration | Đồng bộ tri thức/quyền từ hệ thống khác | Admin/IT, kỹ thuật |

## 5. Luồng Chuẩn Cần Hướng Dẫn Khách Hàng

### 5.1 Chuẩn Bị Tổ Chức

Trước khi khách hàng dùng hệ thống, team triển khai cần giúp họ xác định:

- Team nào tham gia đầu tiên.
- Ai là pilot owner.
- Ai là reviewer của từng team.
- Ai là Admin/IT phụ trách tài khoản và quyền.
- Folder đầu tiên nên tạo.
- Loại tài liệu được phép upload.
- Loại tài liệu không được upload.
- KPI sẽ dùng để đánh giá pilot.

Checklist tư vấn:

- Có 1-2 team pilot rõ ràng.
- Có ít nhất 1 reviewer cho mỗi team.
- Có 30-50 tài liệu đầu tiên.
- Có quy tắc bảo mật dữ liệu trước khi upload.
- Có lịch review feedback.
- Có tiêu chí quyết định mở rộng sau pilot.

### 5.2 Tạo User, Team, Folder Và Quyền

Giải thích cho khách hàng:

- Team là nhóm người dùng theo tổ chức hoặc phạm vi làm việc.
- Folder là nơi chứa tài liệu.
- Quyền được cấp ở folder để user chỉ xem đúng tài liệu được phép.
- Nên cấp quyền theo team trước, quyền riêng theo user chỉ dùng cho ngoại lệ.

Ví dụ cấu trúc folder:

```text
/Support
/Support/FAQ
/Support/Refund
/Operations
/Operations/Onboarding
/Engineering
```

Lỗi tư vấn thường gặp:

- Tạo quá nhiều folder nhỏ ngay từ đầu.
- Cấp quyền company-wide cho tài liệu chỉ dành cho một team.
- Không chỉ định owner/reviewer cho folder.
- Cho user upload trước khi phân quyền xong.

### 5.3 Upload Tài Liệu

Thông điệp cần nhấn mạnh với user:

- Upload không có nghĩa AI dùng ngay.
- Tài liệu phải được reviewer approve.
- Sau approve, hệ thống cần xử lý/index xong.
- Chọn đúng folder là rất quan trọng vì folder quyết định quyền xem và hỏi AI.

Tài liệu nên upload:

- Quy trình nội bộ.
- FAQ.
- Hướng dẫn xử lý sự cố.
- Checklist vận hành.
- Tài liệu onboarding.
- Chính sách nội bộ đã được phép dùng trong pilot.

Tài liệu không nên upload nếu chưa được duyệt:

- Secret, token, API key, password.
- Thông tin nhân sự nhạy cảm.
- Hợp đồng hoặc dữ liệu khách hàng nhạy cảm.
- Tài liệu mật chưa được phép chia sẻ.
- File scan ảnh nếu hệ thống chưa hỗ trợ OCR.

### 5.4 Duyệt Tài Liệu

Reviewer nên approve khi:

- Tài liệu đúng phạm vi.
- Nội dung còn hiệu lực.
- Có owner hoặc nguồn rõ.
- Không chứa thông tin cấm upload.
- Đặt đúng folder.
- File đọc được và có định dạng hỗ trợ.

Reviewer nên reject khi:

- Tài liệu sai folder.
- Nội dung cũ hoặc chưa xác minh.
- Có dữ liệu nhạy cảm.
- File lỗi hoặc không đọc được.
- Người upload cần bổ sung nguồn hoặc chỉnh tên/mô tả.

Khi đào tạo reviewer, cần nói rõ:

```text
Reviewer là lớp kiểm soát để AI chỉ học từ nội dung đã được tổ chức chấp nhận.
```

### 5.5 Hỏi AI

Hướng dẫn user đặt câu hỏi:

- Viết câu hỏi cụ thể.
- Chọn scope phù hợp nếu biết folder/document.
- Tránh hỏi quá rộng.
- Đọc citation trước khi dùng câu trả lời cho việc quan trọng.
- Feedback Incorrect nếu câu trả lời hoặc nguồn không đúng.

Ví dụ câu hỏi tốt:

```text
Điều kiện hoàn tiền đơn hàng trong vòng 7 ngày là gì?
Khi khách báo lỗi đăng nhập, support cần kiểm tra những bước nào?
Version mới của tài liệu chưa duyệt có được AI dùng không?
```

Ví dụ câu hỏi chưa tốt:

```text
Nói hết về quy trình công ty.
Cái này xử lý sao?
Tóm tắt mọi thứ trong folder Support.
```

Giải thích scope:

- `All`: hỏi trên tất cả tài liệu/wiki user có quyền.
- `Folder`: phù hợp khi câu hỏi thuộc một team/chủ đề.
- `Document`: phù hợp khi cần câu trả lời chính xác từ một tài liệu cụ thể.

### 5.6 Đọc Citation

Khi user nhận câu trả lời, hướng dẫn họ kiểm tra:

1. Nguồn có đúng chủ đề không.
2. Folder có đúng phạm vi không.
3. Đoạn trích có hỗ trợ trực tiếp cho câu trả lời không.
4. Nguồn là document hay wiki.
5. Câu trả lời có bỏ sót điều kiện/ngoại lệ quan trọng không.

Nếu câu trả lời nghe hợp lý nhưng citation không liên quan, user nên đánh dấu Incorrect.

### 5.7 Gửi Và Xử Lý Feedback

User chọn Correct khi:

- Câu trả lời đúng.
- Citation phù hợp.
- Câu trả lời giải quyết được câu hỏi.

User chọn Incorrect khi:

- Câu trả lời sai.
- Thiếu điều kiện quan trọng.
- Citation không liên quan.
- AI trả lời quá tự tin dù thiếu nguồn.
- User cần reviewer kiểm tra lại.

Reviewer xử lý feedback theo thứ tự:

1. Đọc câu hỏi.
2. Đọc câu trả lời.
3. Kiểm tra citations.
4. Đọc ghi chú user.
5. Phân loại nguyên nhân.
6. Quyết định hành động: cập nhật tài liệu, tạo wiki, thêm correction, hoặc báo lỗi kỹ thuật.

### 5.8 Tạo Và Publish Wiki

Khi tư vấn, nên giải thích wiki bằng ngôn ngữ đơn giản:

```text
Document là nguồn gốc. Wiki là bản tri thức đã được chuẩn hóa và reviewer xác nhận.
```

Nên tạo wiki khi:

- Tài liệu gốc dài.
- Nội dung hay được hỏi.
- Cần câu trả lời ổn định hơn.
- Có quy trình hoặc FAQ rõ.
- Reviewer muốn tạo nguồn chính thức dễ đọc hơn.

Không nên publish wiki khi:

- Draft thêm thông tin không có trong tài liệu gốc.
- Nội dung nhạy cảm chưa được phép chia sẻ rộng.
- Folder/visibility chưa đúng.
- Reviewer chưa đọc lại nội dung.

## 6. Cách Demo Cho Khách Hàng

### 6.1 Demo Ngắn 15 Phút

Mục tiêu: cho khách hàng thấy giá trị cốt lõi.

Kịch bản:

1. Đăng nhập bằng user thường.
2. Hỏi AI một câu khi chưa có wiki hoặc với tài liệu gốc.
3. Cho xem câu trả lời và citation.
4. Đăng nhập reviewer.
5. Upload hoặc approve một tài liệu mẫu.
6. Generate wiki draft.
7. Publish wiki.
8. Hỏi lại cùng câu hỏi.
9. So sánh câu trả lời/citation trước và sau khi có wiki.
10. Gửi feedback Incorrect cho một câu trả lời chưa tốt và cho xem reviewer queue.

Thông điệp kết thúc:

```text
Giá trị không nằm ở việc AI trả lời thay con người hoàn toàn, mà ở quy trình biến tài liệu được duyệt thành tri thức có thể hỏi, kiểm chứng và cải thiện.
```

### 6.2 Demo Đầy Đủ 45-60 Phút

Phần 1 - Bối cảnh:

- Vấn đề: tài liệu rải rác, hỏi đáp lặp lại, onboarding chậm.
- Cách tiếp cận: quản trị tri thức + reviewer + AI Q&A + citation.

Phần 2 - Admin setup:

- User/team/role.
- Folder.
- Permission.

Phần 3 - Document lifecycle:

- Upload.
- Pending review.
- Approve.
- Processing/indexed.
- Version mới chưa thay version hiện tại cho đến khi được approve/index.

Phần 4 - AI Q&A:

- Hỏi theo All.
- Hỏi theo Folder.
- Hỏi theo Document.
- Đọc citation.
- Gửi feedback.

Phần 5 - Wiki:

- Generate draft.
- Reviewer kiểm tra.
- Publish.
- Hỏi lại để thấy wiki là nguồn tri thức chuẩn hóa.

Phần 6 - Vận hành:

- Dashboard/KPI.
- Feedback queue.
- Audit log.
- Quy tắc dữ liệu.
- Pilot plan.

## 7. Checklist Đào Tạo Theo Vai Trò

### 7.1 User

Sau buổi đào tạo, user cần làm được:

- Đăng nhập.
- Tìm đúng màn hình tài liệu.
- Upload tài liệu đúng folder.
- Hiểu tài liệu cần được duyệt trước khi AI dùng.
- Đặt câu hỏi rõ ràng.
- Chọn scope All/Folder/Document.
- Đọc citation.
- Gửi feedback Correct/Incorrect.
- Biết báo ai khi không thấy tài liệu hoặc câu trả lời sai.

### 7.2 Reviewer

Sau buổi đào tạo, reviewer cần làm được:

- Xem hàng chờ tài liệu.
- Approve/reject đúng tiêu chí.
- Hiểu trạng thái Approved và Indexed khác nhau.
- Generate wiki draft.
- Kiểm tra wiki trước khi publish.
- Publish/reject wiki.
- Xử lý feedback Incorrect.
- Xác định khi nào cần thêm tài liệu, tạo wiki hoặc báo lỗi kỹ thuật.

### 7.3 Admin/IT

Sau buổi đào tạo, Admin/IT cần làm được:

- Tạo user.
- Gán role.
- Tạo team.
- Tạo folder.
- Cấp quyền folder.
- Kiểm tra lỗi đăng nhập/quyền.
- Hiểu audit log.
- Biết cách phối hợp backup và vận hành.
- Biết khi nào cần escalte cho team kỹ thuật.

### 7.4 Pilot Owner

Sau buổi đào tạo, pilot owner cần làm được:

- Chọn team pilot.
- Chọn reviewer.
- Chuẩn bị danh sách tài liệu.
- Theo dõi KPI.
- Tổ chức weekly review.
- Tổng hợp feedback.
- Ra quyết định mở rộng/dừng/cải tiến sau pilot.

## 8. Kịch Bản Onboarding Khách Hàng

### Giai Đoạn 1 - Khảo Sát

Câu hỏi cần hỏi khách hàng:

- Team nào có nhiều câu hỏi lặp lại nhất?
- Tài liệu hiện đang nằm ở đâu?
- Ai là owner của tài liệu?
- Ai có quyền duyệt tài liệu?
- Có dữ liệu nào tuyệt đối không được upload?
- Có yêu cầu phân quyền theo team/folder không?
- KPI nào chứng minh pilot thành công?

Kết quả cần có:

- Danh sách team pilot.
- Danh sách reviewer.
- Danh sách user.
- Folder structure đề xuất.
- Danh sách tài liệu đầu tiên.
- Quy tắc bảo mật dữ liệu.

### Giai Đoạn 2 - Chuẩn Bị Dữ Liệu

Hướng dẫn khách hàng:

- Ưu tiên 30-50 tài liệu có giá trị cao.
- Chọn tài liệu đang còn hiệu lực.
- Đặt tên file rõ nghĩa.
- Tách tài liệu quá dài theo chủ đề.
- Loại bỏ secret, password, dữ liệu nhạy cảm.
- Xác định owner cho từng nhóm tài liệu.

### Giai Đoạn 3 - Cấu Hình Hệ Thống

Team triển khai phối hợp Admin/IT:

- Tạo user.
- Tạo team.
- Tạo folder.
- Cấp quyền.
- Kiểm tra đăng nhập.
- Upload bộ tài liệu mẫu.
- Approve/index thử.
- Hỏi AI kiểm tra nguồn.

### Giai Đoạn 4 - Đào Tạo Người Dùng

Nội dung buổi đào tạo:

- Sản phẩm giải quyết vấn đề gì.
- Vai trò user/reviewer/admin.
- Quy tắc dữ liệu.
- Cách upload.
- Cách hỏi AI.
- Cách đọc citation.
- Cách feedback.
- Cách báo lỗi.

### Giai Đoạn 5 - Theo Dõi Pilot

Nhịp vận hành khuyến nghị:

- Tuần đầu: check hằng ngày các lỗi đăng nhập/quyền/upload.
- Hằng tuần: review dashboard, feedback sai, tài liệu cần thêm, wiki cần publish.
- Cuối pilot: báo cáo KPI và đề xuất bước tiếp theo.

## 9. Câu Hỏi Tư Vấn Thường Gặp

AI có luôn đúng không?

Không. AI có thể sai. Hệ thống giảm rủi ro bằng cách chỉ dùng nguồn đã duyệt, có citation, có phân quyền và có feedback loop.

Tài liệu mới upload có dùng ngay không?

Không. Tài liệu phải được reviewer approve và hệ thống index xong.

Vì sao cần reviewer?

Reviewer đảm bảo AI chỉ dùng nội dung đã được tổ chức kiểm soát, tránh tài liệu sai, cũ hoặc nhạy cảm.

Vì sao cần wiki nếu đã có tài liệu?

Tài liệu gốc thường dài, rời rạc hoặc không tối ưu cho hỏi đáp. Wiki là bản tri thức đã chuẩn hóa để user và AI dùng ổn định hơn.

AI có vượt quyền để tìm câu trả lời không?

Không. Thiết kế yêu cầu backend kiểm tra quyền và trạng thái nguồn trước khi đưa nội dung vào prompt.

Nếu user không có quyền với tài liệu liên quan thì sao?

AI không được dùng tài liệu đó cho câu trả lời của user. Admin/IT cần cấp quyền nếu user thật sự cần xem.

Có thể upload toàn bộ tài liệu công ty ngay không?

Không nên trong pilot. Nên bắt đầu với phạm vi nhỏ, dữ liệu sạch, reviewer rõ ràng và KPI cụ thể.

Khách hàng nên bắt đầu với team nào?

Nên chọn team có nhiều tài liệu quy trình, FAQ hoặc câu hỏi lặp lại, ví dụ Support, Operations, CSKH, Onboarding hoặc IT helpdesk.

Làm sao biết pilot thành công?

Xem số user active, số câu hỏi, tỷ lệ feedback đúng/sai, số feedback sai được xử lý, số wiki published, top nguồn được dùng và nhận xét định tính từ user/reviewer.

## 10. Lỗi Và Tình Huống Cần Escalate

Team triển khai có thể tự xử lý hoặc hướng dẫn khách hàng:

- User quên mật khẩu.
- User chưa được gán đúng role.
- User không thấy folder do thiếu quyền.
- Tài liệu bị reject vì sai folder hoặc nội dung chưa rõ.
- User hỏi quá rộng nên AI không trả lời tốt.
- Citation không đúng và cần reviewer xử lý feedback.

Cần escalate cho Admin/IT:

- Lỗi đăng nhập hàng loạt.
- Folder/quyền bị cấu hình sai trên diện rộng.
- Có nghi ngờ upload nhầm dữ liệu nhạy cảm.
- Cần xóa/khóa quyền truy cập gấp.
- Cần kiểm tra audit log.

Cần escalate cho team kỹ thuật:

- ChromaDB/API không hoạt động.
- Tài liệu approved nhưng mãi không indexed.
- Smoke test fail.
- AI provider lỗi hoặc timeout liên tục.
- Citation trả về nguồn user không nên thấy.
- Có nghi ngờ lỗi phân quyền nghiêm trọng.

## 11. Checklist Go-Live Pilot

Trước kickoff:

- [ ] Có pilot owner.
- [ ] Có danh sách user.
- [ ] Có danh sách reviewer.
- [ ] Có Admin/IT phụ trách.
- [ ] Có folder structure.
- [ ] Có phân quyền folder.
- [ ] Có 30-50 tài liệu đầu tiên.
- [ ] Có quy tắc tài liệu không được upload.
- [ ] Có tài liệu mẫu đã upload, approve và indexed.
- [ ] Có ít nhất một wiki published để demo.
- [ ] Có kịch bản demo.
- [ ] Có kênh báo lỗi/support.

Trong pilot:

- [ ] Theo dõi user active.
- [ ] Theo dõi câu hỏi AI.
- [ ] Theo dõi feedback Incorrect.
- [ ] Reviewer xử lý feedback định kỳ.
- [ ] Tạo wiki cho nội dung hay được hỏi.
- [ ] Ghi nhận lỗi quyền/upload/login.
- [ ] Review KPI hằng tuần.

Sau pilot:

- [ ] Tổng hợp KPI.
- [ ] Tổng hợp feedback user/reviewer.
- [ ] Xác định top use cases có giá trị.
- [ ] Xác định rủi ro còn lại.
- [ ] Đề xuất cải tiến.
- [ ] Đề xuất mở rộng hoặc dừng.

## 12. Bài Kiểm Tra Nhanh Cho Team Triển Khai

Team triển khai nên tự trả lời được các câu sau trước khi đi đào tạo khách hàng:

1. Vì sao tài liệu mới upload chưa được AI dùng ngay?
2. Approved khác Indexed thế nào?
3. Khi nào user nên chọn scope Document thay vì All?
4. Vì sao citation quan trọng?
5. Khi nào feedback Incorrect nên dẫn tới tạo wiki?
6. Vì sao vector store không phải source of truth về quyền?
7. Ai có quyền publish wiki?
8. Cần làm gì nếu khách hàng upload nhầm tài liệu nhạy cảm?
9. Nên bắt đầu pilot với bao nhiêu tài liệu?
10. Dấu hiệu nào cho thấy cần escalate cho team kỹ thuật?

Đáp án kỳ vọng:

- Tài liệu phải qua reviewer và index để đảm bảo kiểm soát chất lượng.
- Approved là được duyệt; Indexed là đã xử lý xong và có thể retrieval.
- Scope Document dùng khi câu hỏi cần chính xác theo một tài liệu cụ thể.
- Citation giúp kiểm chứng câu trả lời.
- Tạo wiki khi tài liệu hay được hỏi, dài, rối hoặc cần chuẩn hóa.
- Quyền phải theo SQLite/folder/user, vector metadata chỉ hỗ trợ filter.
- Reviewer publish wiki.
- Báo reviewer/admin ngay, reject hoặc khóa quyền tùy trạng thái, kiểm tra audit nếu cần.
- Pilot nên bắt đầu với 30-50 tài liệu có giá trị cao.
- Escalate khi lỗi hệ thống, index, AI provider, hoặc rủi ro phân quyền.

## 13. Tài Liệu Liên Quan

- [Hướng dẫn pilot và sử dụng](../pilot/HƯỚNG_DẪN_PILOT_VÀ_SỬ_DỤNG.md)
- [Hướng dẫn trình bày và demo](../presentation/HƯỚNG_DẪN_TRÌNH_BÀY_VÀ_DEMO.md)
- [So sánh có và không có wiki](../presentation/SO_SÁNH_CÓ_VÀ_KHÔNG_CÓ_WIKI.md)
- [Technical system overview](../technical/TỔNG_QUAN_KỸ_THUẬT_CHO_TEAM_VÀ_AI.md)
- [Architecture MVP](../../KIẾN_TRÚC_MVP.md)
- [Data model](../../MÔ_HÌNH_DỮ_LIỆU.md)
- [Troubleshooting](../technical/KHẮC_PHỤC_LỖI.md)
