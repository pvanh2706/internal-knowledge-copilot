# Hướng Dẫn Trình Bày Và Demo Internal Knowledge Copilot

Tài liệu này là tài liệu chính cho việc trình bày với sếp, chuẩn bị slide và chạy demo. Nội dung đã gộp từ boss presentation, slide outline, demo script, demo checklist và pilot plan.

## 1. Thông Điệp Chính

Internal Knowledge Copilot là một MVP giúp công ty tập trung tri thức nội bộ, cho nhân viên hỏi đáp bằng tiếng Việt trên tài liệu đã duyệt, có nguồn trích dẫn rõ ràng và có reviewer kiểm soát chất lượng nội dung.

Đề xuất hiện tại không phải triển khai toàn công ty ngay, mà là chạy pilot có kiểm soát với một vài team trong 2-4 tuần để đo hiệu quả thực tế.

## 2. Vấn Đề Hiện Tại

- Tài liệu nằm rải rác trong nhiều folder, file, kênh trao đổi và người nắm giữ.
- Nhân sự mới mất nhiều thời gian để tìm đúng quy trình, tài liệu, câu trả lời.
- Các team thường hỏi lặp lại những câu giống nhau.
- Tài liệu cũ, trùng lặp hoặc không rõ bản nào là bản đúng để tham chiếu.
- Nếu dùng AI trực tiếp mà không có nguồn và reviewer, rủi ro trả lời sai hoặc dùng tài liệu chưa duyệt.

## 3. Mục Tiêu Thử Nghiệm

- Tập trung tài liệu nội bộ quan trọng vào một hệ thống có phân quyền.
- Cho nhân viên hỏi đáp bằng tiếng Việt trên phạm vi tài liệu được phép truy cập.
- Câu trả lời AI phải có nguồn trích dẫn để người dùng kiểm chứng.
- Chỉ tài liệu đã được reviewer approve mới được đưa vào AI Q&A.
- Biến tài liệu gốc thành wiki nội bộ đã được reviewer publish.
- Có dashboard và feedback để đo chất lượng câu trả lời trong quá trình pilot.

## 4. Giải Pháp MVP Đã Có

- Đăng nhập, quản lý user, role và team.
- Quản lý folder và phân quyền theo team/user.
- Upload tài liệu PDF, DOCX, Markdown và TXT.
- Reviewer approve/reject tài liệu trước khi đưa vào tri thức AI.
- Versioning tài liệu, bản mới chưa duyệt không thay thế bản hiện tại.
- AI Q&A có nguồn trích dẫn.
- Feedback đúng/sai cho câu trả lời AI.
- Reviewer queue để xử lý feedback sai.
- Sinh wiki draft từ tài liệu đã duyệt.
- Reviewer publish wiki để tạo lớp tri thức chuẩn hóa.
- Dashboard KPI và audit log cho Admin/Reviewer.

## 5. Giá Trị Kỳ Vọng

- Giảm thời gian tìm kiếm tài liệu và câu trả lời nội bộ.
- Giảm việc hỏi đáp lặp lại giữa các team.
- Tăng tốc onboarding cho nhân sự mới.
- Chuẩn hóa tri thức từ tài liệu gốc sang wiki nội bộ.
- Kiểm soát tốt hơn việc AI dùng tài liệu nào để trả lời.
- Giữ được quyền truy cập theo team/folder.
- Có số liệu để đánh giá: số câu hỏi, tỷ lệ feedback sai, nguồn được dùng nhiều, tài liệu cần chuẩn hóa.

## 6. Dàn Ý Slide 10 Trang

Slide 1 - Internal Knowledge Copilot:

- Subtitle: Thử nghiệm trợ lý tri thức nội bộ cho các team trong công ty.
- Hỏi đáp trên tài liệu nội bộ đã duyệt.
- Câu trả lời có nguồn trích dẫn.
- Có reviewer kiểm soát chất lượng tri thức.

Slide 2 - Vấn đề hiện tại:

- Tài liệu rải rác, khó tìm.
- Câu hỏi nội bộ bị lặp lại.
- Nhân sự mới onboarding chậm.
- Khó biết tài liệu nào là bản đúng.
- Dùng AI không có nguồn sẽ có rủi ro.

Slide 3 - Mục tiêu thử nghiệm:

- Tập trung tài liệu quan trọng.
- Hỏi đáp bằng tiếng Việt.
- Câu trả lời có citation.
- Chỉ dùng tài liệu đã approve.
- Tạo wiki chuẩn hóa từ tài liệu gốc.
- Đo KPI và feedback trong pilot.

Slide 4 - Giải pháp MVP:

- User upload tài liệu.
- Reviewer approve/reject.
- AI Q&A có nguồn.
- Feedback đúng/sai.
- Reviewer xử lý feedback.
- Generate và publish wiki.
- Dashboard và audit log.

Slide 5 - Cách hệ thống hoạt động:

```text
Tài liệu nội bộ
-> Reviewer approve
-> Index vào kho tri thức
-> User hỏi AI
-> AI trả lời có nguồn
-> User feedback
-> Reviewer cải thiện wiki
```

Slide 6 - Demo nghiệp vụ:

- Upload tài liệu.
- Approve tài liệu.
- Hỏi AI và xem citation.
- Gửi feedback sai.
- Reviewer xem queue.
- Generate/publish wiki.
- Xem dashboard.

Slide 7 - Giá trị kỳ vọng:

- Tìm câu trả lời nhanh hơn.
- Giảm hỏi đáp lặp lại.
- Onboarding nhanh hơn.
- Tri thức được chuẩn hóa.
- Có quyền truy cập và audit.
- Đo được hiệu quả bằng KPI.

Slide 8 - Phạm vi pilot đề xuất:

- 1-2 team đầu tiên.
- 30-50 tài liệu quan trọng.
- 1 reviewer mỗi team.
- Chạy 2-4 tuần.
- Review KPI hằng tuần.

Slide 9 - Rủi ro và kiểm soát:

- AI sai: citation, feedback, reviewer queue.
- Lộ thông tin: phân quyền folder/team/user.
- Tài liệu chưa duyệt: không dùng cho AI.
- MVP chưa production lớn: pilot giới hạn trước.

Slide 10 - Đề xuất quyết định:

- Phê duyệt pilot.
- Chọn team tham gia.
- Chỉ định reviewer.
- Chọn bộ tài liệu thử nghiệm.
- Thống nhất KPI thành công.

## 7. Kịch Bản Demo

Mở đầu:

> Em sẽ demo theo một tình huống đơn giản: một team có tài liệu nội bộ, user cần hỏi đáp trên tài liệu đó, reviewer cần kiểm soát chất lượng trước khi AI được phép sử dụng.

Bước 1 - Admin/Reviewer quản lý folder và quyền:

- Mở màn hình folder/team/user nếu cần.
- Cho thấy tài liệu được đặt trong folder có phân quyền.
- Nhấn mạnh: AI chỉ trả lời trên phạm vi người dùng được phép truy cập.

Bước 2 - User upload tài liệu:

- Đăng nhập bằng User.
- Upload một file TXT/Markdown/PDF/DOCX mẫu.
- Cho thấy tài liệu ở trạng thái chờ duyệt.
- Nhấn mạnh: tài liệu mới upload chưa được đưa vào AI ngay.

Bước 3 - Reviewer approve tài liệu:

- Đăng nhập bằng Reviewer.
- Mở document review queue.
- Approve tài liệu.
- Cho thấy trạng thái xử lý/index nếu có.
- Nhấn mạnh: chỉ tài liệu đã approve mới được đưa vào kho tri thức.

Bước 4 - User hỏi AI:

- Đăng nhập lại bằng User.
- Mở trang AI Q&A.
- Chọn scope phù hợp.
- Hỏi câu liên quan đến tài liệu vừa approve.
- Nhấn mạnh: người dùng hỏi bằng tiếng Việt, AI trả lời dựa trên tài liệu user có quyền xem.

Bước 5 - Xem citation:

- Chỉ vào phần nguồn/citation trong câu trả lời.
- Nhấn mạnh: người dùng thấy được câu trả lời dựa trên nguồn nào, title nào, folder nào và đoạn trích liên quan.

Bước 6 - User gửi feedback:

- Đánh dấu câu trả lời là Incorrect.
- Thêm ghi chú ngắn.
- Nhấn mạnh: feedback vào queue của reviewer thay vì bị mất trong chat.

Bước 7 - Reviewer xử lý feedback:

- Đăng nhập Reviewer.
- Mở feedback review queue.
- Cho thấy câu hỏi, câu trả lời, nguồn, ghi chú user.
- Đổi status nếu cần.

Bước 8 - Generate và publish wiki:

- Mở tài liệu đã approve.
- Generate wiki draft.
- Mở danh sách/wiki draft.
- Publish wiki.
- Nhấn mạnh: wiki published là lớp tri thức chuẩn hóa và có thể được ưu tiên khi AI trả lời.

Bước 9 - Dashboard và audit:

- Mở dashboard.
- Mở audit log nếu cần.
- Nhấn mạnh: pilot có thể theo dõi số tài liệu, wiki, câu hỏi AI, feedback sai và nguồn được trích dẫn nhiều.

Kết demo:

> Phần demo này cho thấy MVP đã có đầy đủ luồng thử nghiệm: tài liệu vào hệ thống, reviewer kiểm soát, user hỏi đáp có nguồn, feedback để cải thiện, và dashboard để đo hiệu quả. Bước tiếp theo nên là pilot nhỏ với tài liệu thật của 1-2 team.

## 8. Checklist Trước Demo

Nội dung cần chuẩn bị:

- Một bộ slide theo dàn ý ở trên.
- Một tài liệu mẫu để upload.
- Một câu hỏi mẫu có thể trả lời được từ tài liệu.
- Một câu hỏi mẫu không đủ thông tin để AI hỏi lại hoặc phản hồi thiếu context.
- Một feedback Incorrect mẫu.
- Một wiki draft/publish flow mẫu.

Tài khoản demo:

- Admin: quản lý user/team/folder và xem audit.
- Reviewer: approve tài liệu, xử lý feedback, publish wiki.
- User: upload tài liệu, hỏi AI, gửi feedback.

Kiểm tra trước demo:

- Backend chạy được.
- Frontend chạy được.
- ChromaDB chạy được.
- Đăng nhập được 3 role Admin/Reviewer/User.
- Upload tài liệu thành công.
- Reviewer approve tài liệu thành công.
- AI Q&A trả lời có citation.
- Feedback Incorrect vào reviewer queue.
- Generate và publish wiki thành công.
- Dashboard hiện KPI.

Lệnh kiểm tra nhanh:

```powershell
dotnet test src/backend/InternalKnowledgeCopilot.sln
```

```powershell
cd src/frontend
npm run build
npm test
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-mvp.ps1
```

Nguyên tắc khi demo:

- Nói theo vấn đề và giá trị, không nói theo code.
- Demo luồng nghiệp vụ thật của User/Reviewer/Admin.
- Mỗi câu trả lời AI cần chỉ vào citation.
- Khi nói về AI sai, chủ động nói có feedback và reviewer queue.
- Khi nói về bảo mật, nói về phân quyền và việc tài liệu chưa approve không được dùng cho AI.
- Kết thúc bằng đề xuất pilot nhỏ, không đề xuất rollout toàn công ty ngay.

## 9. Phạm Vi Pilot Đề Xuất

Thời gian:

- 2-4 tuần.

Team tham gia:

- 1-2 team đầu tiên.
- Gợi ý: Support, Kỹ thuật, Vận hành, CSKH hoặc team có nhiều tài liệu quy trình.

Dữ liệu:

- 30-50 tài liệu nội bộ quan trọng.
- Ưu tiên tài liệu hay được hỏi lại, quy trình lặp lại, FAQ, hướng dẫn xử lý sự cố, onboarding.

Người tham gia:

- 1 owner pilot.
- 1 reviewer mỗi team.
- 5-15 user dùng thử mỗi team.
- 1 người kỹ thuật hỗ trợ vận hành trong thời gian pilot.

KPI theo dõi:

- Số câu hỏi AI theo ngày/tuần.
- Số user active trong pilot.
- Số tài liệu upload.
- Số tài liệu approved/rejected.
- Số wiki draft generated.
- Số wiki published.
- Số feedback Correct/Incorrect.
- Số incorrect feedback đã xử lý.
- Top cited sources.
- Thời gian trung bình từ upload đến approve.

## 10. Câu Hỏi Có Thể Gặp

AI có trả lời sai không?

Có thể có, nên MVP không để AI trả lời vô kiểm soát. Câu trả lời có nguồn, user có feedback đúng/sai, reviewer có queue để xử lý, và tài liệu chưa duyệt không được dùng cho AI.

Có lộ tài liệu nội bộ không?

Pilot đề xuất chạy nội bộ, có phân quyền theo folder/team/user. Backend kiểm tra quyền trước khi dùng nguồn cho AI, và file upload không nằm trong public web root.

Có nên triển khai toàn công ty ngay không?

Chưa nên. Nên pilot với 1-2 team, đo KPI và feedback trong 2-4 tuần, sau đó mới quyết định mở rộng.

Cần gì để bắt đầu?

Cần chọn team pilot, reviewer của team, bộ tài liệu nội bộ đầu tiên, thời gian pilot và KPI thành công.

## 11. Quyết Định Cần Xin

- Có cho phép chạy pilot hay không.
- Team nào tham gia đầu tiên.
- Ai là reviewer của từng team.
- Bộ tài liệu nào được đưa vào pilot.
- KPI nào dùng để đánh giá thành công.
- Sau pilot, ai là người quyết định mở rộng, tiếp tục chỉnh sửa hoặc dừng.
