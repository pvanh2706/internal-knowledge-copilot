# Hướng Dẫn Pilot Và Sử Dụng Internal Knowledge Copilot

Tài liệu này là hướng dẫn chính cho giai đoạn pilot. Nội dung đã gộp từ các tài liệu hướng dẫn User, Reviewer, Admin, onboarding, FAQ, bảo mật dữ liệu, chuẩn bị nội dung và vận hành pilot.

## 1. Mục Tiêu Pilot

Pilot dùng để kiểm chứng Internal Knowledge Copilot có giúp team:

- Tìm câu trả lời nội bộ nhanh hơn.
- Giảm việc hỏi đáp lặp lại.
- Onboarding nhân sự mới tốt hơn.
- Chuẩn hóa tài liệu thành wiki nội bộ.
- Kiểm soát việc AI trả lời dựa trên nguồn nào.

Đây là giai đoạn thử nghiệm có kiểm soát, chưa phải triển khai chính thức cho toàn công ty.

## 2. Phạm Vi Đề Xuất

Thời gian:

- 2-4 tuần.

Team tham gia:

- 1-2 team đầu tiên.
- Ưu tiên team có nhiều tài liệu quy trình, FAQ hoặc câu hỏi lặp lại như Support, Kỹ thuật, Vận hành, CSKH.

Dữ liệu:

- 30-50 tài liệu nội bộ quan trọng.
- Ưu tiên tài liệu hay được hỏi lại, quy trình lặp lại, FAQ, hướng dẫn xử lý sự cố và onboarding.

Người tham gia:

- 1 pilot owner.
- 1 reviewer mỗi team.
- 5-15 user dùng thử mỗi team.
- 1 người kỹ thuật hỗ trợ vận hành trong thời gian pilot.

## 3. Vai Trò Trong Hệ Thống

User:

- Xem tài liệu trong phạm vi được cấp quyền.
- Upload tài liệu để reviewer duyệt.
- Hỏi AI trên tài liệu/wiki đã được duyệt.
- Xem nguồn trích dẫn của câu trả lời.
- Gửi feedback Correct hoặc Incorrect.

Reviewer:

- Approve hoặc reject tài liệu.
- Kiểm tra trạng thái xử lý/index tài liệu.
- Xử lý feedback Incorrect.
- Generate wiki draft từ tài liệu đã approve.
- Publish hoặc reject wiki draft.
- Xem dashboard.

Admin/IT:

- Tạo và cập nhật user.
- Gán role Admin, Reviewer, User.
- Quản lý team, folder và phân quyền.
- Xem dashboard, audit log.
- Hỗ trợ lỗi đăng nhập, quyền và vận hành hệ thống.

Pilot owner:

- Theo dõi lịch pilot.
- Điều phối reviewer, user và IT support.
- Tổng hợp KPI và feedback.
- Báo cáo kết quả sau pilot.

## 4. Quy Tắc Dữ Liệu Và Bảo Mật

Nguyên tắc chung:

- Chỉ upload tài liệu được phép dùng trong pilot.
- Chỉ cấp quyền cho người cần truy cập.
- Tài liệu chưa duyệt không được dùng cho AI Q&A.
- Tài liệu bị reject không được dùng cho AI Q&A.
- User phải kiểm tra citation trước khi dùng câu trả lời cho việc quan trọng.

Dữ liệu nên upload:

- Quy trình nội bộ trong phạm vi team.
- FAQ nội bộ.
- Hướng dẫn vận hành.
- Hướng dẫn onboarding.
- Tài liệu sản phẩm/dịch vụ đã được phép chia sẻ trong team.
- Checklist và playbook nội bộ.

Dữ liệu không nên upload trong pilot nếu chưa có phê duyệt rõ:

- Dữ liệu cá nhân nhạy cảm.
- Thông tin lương thưởng, kỷ luật, hồ sơ nhân sự.
- Hợp đồng, điều khoản pháp lý nhạy cảm.
- Thông tin khách hàng có tính bảo mật cao.
- Secret, API key, password, token.
- Tài liệu mật của công ty.
- Tài liệu mà team không có quyền chia sẻ.

Nếu upload nhầm tài liệu:

1. Báo ngay cho Reviewer và Admin.
2. Nếu tài liệu chưa approve, reject tài liệu.
3. Nếu tài liệu đã approve, tạm dừng quyền truy cập liên quan nếu cần.
4. Kiểm tra tài liệu đã được dùng trong câu trả lời AI chưa.
5. Ghi nhận sự cố và hành động khắc phục.

## 5. Chuẩn Bị Nội Dung Trước Khi Upload

Nội dung đầu vào càng rõ thì AI trả lời càng có ích. Pilot nên bắt đầu với bộ tài liệu nhỏ, đúng, có owner và hay được hỏi trong công việc.

Loại tài liệu nên chọn:

- FAQ nội bộ.
- Quy trình làm việc.
- Hướng dẫn xử lý sự cố.
- Hướng dẫn onboarding.
- Chính sách nội bộ trong phạm vi team.
- Checklist vận hành.
- Tài liệu sản phẩm/dịch vụ hay được hỏi.

Định dạng file hỗ trợ:

- PDF.
- DOCX.
- Markdown.
- TXT.

Khuyến nghị:

- Dùng Markdown hoặc DOCX nếu có thể.
- PDF nên là PDF có text, không phải ảnh scan.
- TXT/Markdown nên có heading rõ.
- Tách tài liệu quá dài thành nhiều tài liệu theo chủ đề.

Tên file nên rõ nghĩa:

```text
refund-policy-v1.md
support-ticket-priority-guide.docx
new-employee-onboarding-checklist.pdf
```

Tránh tên file mơ hồ:

```text
final.docx
new_final_2.pdf
abc.txt
quytrinh.docx
```

Cấu trúc tài liệu nên có:

- Tiêu đề rõ.
- Mục đích.
- Phạm vi áp dụng.
- Đối tượng áp dụng.
- Các bước thực hiện.
- Điều kiện/ngoại lệ.
- Người hoặc team phụ trách.
- Ngày cập nhật.

Checklist trước khi upload:

- Tài liệu còn hiệu lực.
- Có người chịu trách nhiệm nội dung.
- Không chứa dữ liệu cấm upload.
- Định dạng file được hỗ trợ.
- Tên file rõ nghĩa.
- Folder đúng.
- Reviewer của team đã biết tài liệu này sẽ được upload.

## 6. Đăng Nhập

1. Mở đường dẫn hệ thống do Admin hoặc IT cung cấp.
2. Nhập email và mật khẩu.
3. Nếu hệ thống yêu cầu đổi mật khẩu lần đầu, nhập mật khẩu mới.
4. Sau khi đăng nhập, kiểm tra dashboard và các menu được cấp quyền.

Nếu không đăng nhập được:

- Kiểm tra email có đúng tài khoản được tạo trong hệ thống không.
- Kiểm tra mật khẩu.
- Liên hệ Admin hoặc IT để reset mật khẩu nếu cần.

## 7. Upload Tài Liệu Và Version Mới

User có thể upload tài liệu để reviewer duyệt trước khi AI được phép sử dụng.

Các bước upload:

1. Vào màn hình `Documents`.
2. Chọn folder phù hợp với nội dung tài liệu.
3. Nhập tên tài liệu rõ nghĩa.
4. Nhập mô tả nếu cần.
5. Chọn file cần upload.
6. Bấm `Upload`.
7. Kiểm tra tài liệu mới có trạng thái chờ duyệt.

Lưu ý:

- Tài liệu mới upload chưa được AI dùng ngay.
- Tài liệu phải được reviewer approve và hệ thống index xong thì mới dùng được cho Q&A.
- Chọn đúng folder vì folder quyết định phạm vi xem và hỏi AI.
- Không upload tài liệu nhạy cảm nếu chưa được phép dùng trong pilot.

Khi upload version mới:

- Version mới chưa approved không thay thế version hiện tại.
- AI vẫn dùng version hiện tại cho đến khi version mới được approve và index.
- Nếu nội dung thay đổi lớn, reviewer nên xem xét tạo lại wiki từ version mới.

## 8. Duyệt Tài Liệu

Reviewer chỉ approve tài liệu khi:

- Tài liệu đúng phạm vi pilot.
- Tài liệu có owner hoặc nguồn rõ ràng.
- Nội dung còn hiệu lực.
- Không chứa thông tin bị cấm upload trong pilot.
- Đặt đúng folder và phân quyền.
- Định dạng file được hỗ trợ.

Reviewer nên reject tài liệu khi:

- Tài liệu sai folder hoặc sai phạm vi.
- Tài liệu quá cũ, chưa xác minh.
- Tài liệu chứa thông tin nhạy cảm không phù hợp pilot.
- File không đọc được.
- Người upload cần bổ sung lý do hoặc nguồn.

Khi reject, ghi lý do rõ để user biết cần sửa gì.

## 9. Hỏi AI Và Đọc Citation

Khi đặt câu hỏi:

- Viết câu hỏi rõ ràng bằng tiếng Việt.
- Nếu câu hỏi liên quan đến một team/folder cụ thể, chọn scope phù hợp.
- Nếu biết tài liệu cần hỏi, chọn scope theo document để kết quả chính xác hơn.
- Tránh hỏi quá rộng như “nói tất cả về quy trình công ty”.

Scope:

- `All`: hỏi trên tất cả tài liệu/wiki mà user có quyền xem.
- `Folder`: hỏi trong một nhóm tài liệu thuộc folder cụ thể.
- `Document`: hỏi trong một tài liệu cụ thể, phù hợp khi cần câu trả lời chính xác.

Mỗi câu trả lời tốt cần có nguồn trích dẫn. Khi đọc citation, kiểm tra:

- Tên tài liệu/wiki có đúng chủ đề không.
- Folder có đúng phạm vi team không.
- Đoạn trích có liên quan trực tiếp đến câu trả lời không.

Nếu câu trả lời nghe hợp lý nhưng citation không liên quan, hãy feedback Incorrect.

## 10. Gửi Và Xử Lý Feedback

User chọn `Correct` khi:

- Câu trả lời đúng.
- Có nguồn phù hợp.
- Giúp giải quyết câu hỏi.

User chọn `Incorrect` khi:

- Câu trả lời sai.
- Thiếu thông tin quan trọng.
- Citation không liên quan.
- AI trả lời ngoài phạm vi tài liệu.
- Câu trả lời cần reviewer kiểm tra lại.

Khi reviewer xử lý feedback Incorrect:

1. Đọc câu hỏi của user.
2. Đọc câu trả lời AI.
3. Kiểm tra citation AI đã dùng.
4. Đọc ghi chú của user.
5. Phân loại nguyên nhân.

Nguyên nhân thường gặp:

- Tài liệu gốc thiếu thông tin.
- Tài liệu gốc có nói nhưng AI chưa lấy đúng chunk.
- Câu hỏi quá rộng.
- User không có quyền tài liệu liên quan.
- Citation không đúng.
- Cần tạo wiki để chuẩn hóa tri thức.

Sau khi xử lý:

- Cập nhật status feedback.
- Nếu cần, upload/approve tài liệu mới.
- Nếu cần, generate/publish wiki.
- Nếu là lỗi hệ thống, báo Admin/IT support.

## 11. Tạo Và Publish Wiki

Nên generate wiki draft khi:

- Tài liệu gốc dài nhưng nội dung hay được hỏi.
- Tài liệu có quy trình rõ.
- Tài liệu có FAQ hoặc hướng dẫn lặp lại.
- Cần chuẩn hóa nội dung để user đọc nhanh hơn.

Không nên generate wiki draft khi:

- Tài liệu gốc chưa được approve.
- Tài liệu gốc còn tranh cãi.
- Tài liệu quá nhạy cảm.
- Tài liệu không có cấu trúc rõ và cần biên tập thủ công trước.

Trước khi publish wiki:

- Đọc lại toàn bộ draft.
- Đảm bảo draft không thêm thông tin ngoài tài liệu gốc.
- Đảm bảo title rõ ràng.
- Đảm bảo visibility/folder đúng.
- Nếu publish company-wide, phải xác nhận đây là nội dung được chia sẻ rộng.

## 12. Quản Lý Team, Folder Và Phân Quyền

Mỗi team pilot nên có:

- Tên team rõ ràng.
- Ít nhất 1 reviewer.
- Danh sách user tham gia pilot.

Folder nên phân theo cách team làm việc, ví dụ:

```text
/Support
/Support/FAQ
/Support/Refund
/Engineering
/Operations
/Onboarding
```

Nguyên tắc folder:

- Tên folder ngắn, rõ nghĩa.
- Không tạo folder quá sâu nếu chưa cần.
- Mỗi folder nên có owner/reviewer.
- Phân quyền folder trước khi user upload tài liệu.

Nguyên tắc phân quyền:

- Cấp quyền tối thiểu cần thiết.
- Cấp quyền theo team trước, user-specific chỉ dùng khi cần.
- Không cấp company-wide nếu tài liệu chỉ dành cho một team.
- Khi user chuyển team hoặc rời pilot, Admin cần cập nhật quyền.
- Nếu user báo thấy tài liệu không nên thấy, xử lý như sự cố phân quyền.

## 13. Vận Hành Pilot

Tuần 0 - Chuẩn bị:

- Chọn team pilot.
- Chỉ định reviewer.
- Chọn user tham gia.
- Chọn 30-50 tài liệu đầu tiên.
- Tạo folder và phân quyền.
- Upload và approve bộ tài liệu mẫu.
- Chạy demo nội bộ với reviewer.

Tuần 1 - Kickoff và sử dụng có hướng dẫn:

- Giới thiệu mục tiêu pilot cho user.
- Hướng dẫn cách hỏi AI và feedback.
- Theo dõi lỗi đăng nhập/quyền hằng ngày.
- Reviewer xử lý feedback sai mỗi ngày.

Tuần 2 - Chuẩn hóa tri thức:

- Xem top câu hỏi và top cited sources.
- Generate wiki cho tài liệu hay được hỏi.
- Publish wiki sau khi reviewer kiểm tra.
- Ghi nhận câu hỏi AI trả lời chưa tốt.

Tuần 3-4 - Đánh giá:

- Tổng hợp KPI.
- Lấy feedback user/reviewer.
- Xác định cải tiến cần làm.
- Đề xuất tiếp tục, mở rộng hoặc dừng pilot.

Nhịp họp gợi ý:

- Kickoff: 30-45 phút, giới thiệu mục tiêu, cách dùng, quy tắc dữ liệu.
- Daily check trong tuần đầu: 10-15 phút hoặc cập nhật async.
- Weekly review: 30 phút, xem KPI, feedback sai, tài liệu cần thêm, wiki cần publish.
- Final review: 45-60 phút, tổng hợp kết quả và quyết định bước tiếp theo.

## 14. KPI Và Báo Cáo Kết Quả

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

Pilot có tín hiệu tốt nếu:

- User đặt câu hỏi thực tế mỗi tuần.
- Câu trả lời có nguồn và có thể kiểm chứng.
- Tỷ lệ feedback sai ở mức chấp nhận được và giảm dần.
- Reviewer xử lý được feedback sai.
- Có wiki published từ các tài liệu hay được hỏi.
- Không phát sinh lỗi phân quyền nghiêm trọng.
- Team thấy việc tìm thông tin nhanh hơn cách cũ.

Kết quả cần báo cáo sau pilot:

- Tổng số user tham gia.
- Tổng số câu hỏi.
- Tỷ lệ feedback Correct/Incorrect.
- Số feedback sai đã xử lý.
- Số wiki đã publish.
- Top 10 nguồn được hỏi/trích dẫn nhiều.
- Các tình huống AI trả lời tốt.
- Các tình huống AI trả lời chưa tốt.
- Đề xuất cải tiến trước khi mở rộng.
- Đề xuất có/không mở rộng sang team tiếp theo.

## 15. Câu Hỏi Thường Gặp

AI có luôn đúng không?

Không. AI có thể sai hoặc thiếu thông tin. Vì vậy câu trả lời cần có nguồn trích dẫn, user cần đọc citation khi dùng cho việc quan trọng, và feedback Incorrect nếu thấy sai.

AI lấy câu trả lời từ đâu?

AI trả lời dựa trên tài liệu approved và wiki published mà user có quyền truy cập.

Tài liệu mới upload có được AI dùng ngay không?

Không. Tài liệu mới upload phải được reviewer approve và hệ thống xử lý/index xong thì mới được dùng cho AI Q&A.

Tài liệu bị reject có được AI dùng không?

Không. Tài liệu rejected không được dùng cho AI.

Vì sao tôi không thấy tài liệu?

Có thể vì bạn chưa có quyền folder, tài liệu chưa approved, tài liệu nằm sai folder, hoặc tài liệu đã bị xóa/reject. Hãy báo Admin/IT support kèm tên tài liệu/folder.

Vì sao AI không trả lời được?

Có thể vì không có tài liệu liên quan, câu hỏi quá rộng, bạn không có quyền xem tài liệu liên quan, tài liệu liên quan chưa approved/indexed, hoặc nội dung tài liệu không đủ rõ. Thử hỏi lại rõ hơn hoặc chọn scope folder/document cụ thể.

Feedback Incorrect đi đâu?

Feedback Incorrect sẽ vào queue của Reviewer. Reviewer có thể xem câu hỏi, câu trả lời, citation và ghi chú của user để xử lý.

Có nên upload tài liệu nhạy cảm không?

Không upload tài liệu nhạy cảm trong pilot nếu chưa có phê duyệt rõ ràng. Nếu không chắc, hỏi reviewer hoặc pilot owner trước.

AI có xem được tài liệu tôi không có quyền không?

Thiết kế pilot yêu cầu AI chỉ dùng nguồn mà user có quyền truy cập. Backend recheck quyền trước khi dùng nguồn cho câu trả lời.

Khi nào nên tạo wiki?

Nên tạo wiki khi tài liệu gốc dài, nội dung hay được hỏi, cần bản tóm tắt/chuẩn hóa để AI trả lời tốt hơn, hoặc reviewer muốn tạo một nguồn tri thức chính thức hơn tài liệu gốc.

Ai có quyền publish wiki?

Reviewer có quyền publish wiki. User thông thường không publish wiki.

Pilot này có thay thế quy trình hiện tại không?

Chưa. Pilot dùng để thử nghiệm và đo hiệu quả. Các quy trình chính thức hiện tại vẫn giữ nguyên cho đến khi công ty quyết định mở rộng hoặc thay đổi.
