# Hướng Dẫn Sử Dụng Internal Knowledge Copilot

Tài liệu này hướng dẫn thao tác cơ bản cho người dùng, reviewer và admin trong giai đoạn pilot: đăng nhập, upload file, duyệt tài liệu, hỏi AI, xử lý feedback và tạo wiki nội bộ.

## 1. Vai Trò Trong Hệ Thống

Hệ thống có 3 nhóm vai trò chính:

- User: upload tài liệu, xem tài liệu trong phạm vi được cấp quyền, hỏi AI và gửi feedback.
- Reviewer: duyệt hoặc từ chối tài liệu, xử lý feedback sai, tạo và publish wiki.
- Admin: quản lý user, team, folder, phân quyền, cấu hình hệ thống và xem audit log.

Một số màn hình chỉ hiện với đúng vai trò. Nếu không thấy menu hoặc màn hình cần dùng, hãy kiểm tra lại role và quyền folder với Admin.

## 2. Đăng Nhập

1. Mở đường dẫn hệ thống do Admin hoặc IT cung cấp.
2. Nhập email và mật khẩu.
3. Nếu hệ thống yêu cầu đổi mật khẩu lần đầu, nhập mật khẩu mới.
4. Sau khi đăng nhập, kiểm tra dashboard và các menu được cấp quyền.

Nếu không đăng nhập được:

- Kiểm tra email có đúng tài khoản được tạo trong hệ thống không.
- Kiểm tra mật khẩu.
- Liên hệ Admin hoặc IT để reset mật khẩu nếu cần.

## 3. Upload File Tài Liệu

User có thể upload tài liệu để reviewer duyệt trước khi AI được phép sử dụng.

Định dạng file được hỗ trợ:

- PDF.
- DOCX.
- Markdown.
- TXT.

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
- PDF nên là file có text, không phải file scan ảnh chưa OCR.

## 4. Upload Version Mới

Khi tài liệu đã tồn tại nhưng có bản cập nhật:

1. Vào `Documents`.
2. Chọn tài liệu cần cập nhật.
3. Ở phần chi tiết tài liệu, chọn file version mới.
4. Bấm `Upload version`.
5. Chờ reviewer duyệt version mới.

Nguyên tắc version:

- Version mới chưa approved không thay thế version hiện tại.
- AI vẫn dùng version hiện tại cho đến khi version mới được approve và index.
- Nếu nội dung thay đổi lớn, reviewer nên xem xét tạo lại wiki từ version mới.

## 5. Duyệt Tài Liệu

Reviewer duyệt tài liệu trước khi đưa vào kho tri thức.

Các bước duyệt:

1. Đăng nhập bằng tài khoản Reviewer hoặc Admin.
2. Vào màn hình `Review`.
3. Chọn tài liệu hoặc version đang chờ duyệt.
4. Kiểm tra tên, folder, người upload và nội dung file.
5. Chọn approve nếu tài liệu hợp lệ.
6. Chọn reject nếu tài liệu chưa đạt, kèm lý do rõ ràng.

Chỉ approve khi:

- Tài liệu đúng phạm vi pilot.
- Nội dung còn hiệu lực.
- Có owner hoặc nguồn rõ ràng.
- Đúng folder và phạm vi phân quyền.
- Không chứa dữ liệu bị cấm upload.
- File đọc được và thuộc định dạng hỗ trợ.

Reject khi:

- Sai folder hoặc sai phạm vi.
- Tài liệu quá cũ, chưa xác minh.
- File lỗi hoặc không đọc được.
- Có dữ liệu nhạy cảm chưa được phê duyệt.
- Người upload cần bổ sung nguồn, lý do hoặc phiên bản đúng.

Sau khi approve, hệ thống sẽ xử lý nội dung, chia chunk, tạo embedding và index vào kho tri thức. Nếu index chưa xong, AI có thể chưa dùng được tài liệu ngay.

## 6. Hỏi AI Trên Tài Liệu Và Wiki

User hỏi AI tại màn hình `AI`.

Các bước hỏi:

1. Vào màn hình `AI`.
2. Nhập câu hỏi rõ ràng bằng tiếng Việt.
3. Chọn scope phù hợp nếu màn hình có lựa chọn scope.
4. Gửi câu hỏi.
5. Đọc câu trả lời và phần nguồn trích dẫn.
6. Gửi feedback `Correct` hoặc `Incorrect` nếu cần.

Cách chọn scope:

- All: hỏi trên tất cả tài liệu và wiki mà bạn có quyền xem.
- Folder: dùng khi câu hỏi nằm trong một nhóm tài liệu của folder cụ thể.
- Document: dùng khi câu hỏi liên quan trực tiếp đến một tài liệu cụ thể.

Ví dụ câu hỏi tốt:

```text
Quy trình xử lý ticket ưu tiên cao gồm những bước nào?
```

```text
Khi khách hàng yêu cầu hoàn tiền thì cần kiểm tra những điều kiện gì?
```

Nên tránh câu hỏi quá rộng như:

```text
Nói tất cả về quy trình công ty.
```

## 7. Đọc Citation Và Gửi Feedback

Mỗi câu trả lời tốt cần có nguồn trích dẫn.

Khi đọc citation, kiểm tra:

- Tên tài liệu hoặc wiki có đúng chủ đề không.
- Folder có đúng phạm vi team không.
- Đoạn trích có liên quan trực tiếp đến câu trả lời không.

Chọn `Correct` khi:

- Câu trả lời đúng với tài liệu.
- Citation liên quan.
- Nội dung giúp xử lý công việc.

Chọn `Incorrect` khi:

- Câu trả lời sai hoặc thiếu ý quan trọng.
- Citation không liên quan.
- AI nói không có thông tin trong khi tài liệu có.
- AI trả lời quá chung chung.

Khi chọn `Incorrect`, nên ghi chú ngắn:

```text
Câu trả lời thiếu điều kiện số 3 trong quy trình.
```

## 8. Tạo Wiki Từ Tài Liệu

Wiki là bản tri thức đã được chuẩn hóa từ tài liệu gốc. Wiki published sẽ được ưu tiên khi AI trả lời nếu phù hợp.

Reviewer nên tạo wiki khi:

- Tài liệu gốc dài nhưng hay được hỏi.
- Nội dung là quy trình, FAQ, chính sách hoặc hướng dẫn lặp lại.
- Cần một bản tóm tắt có cấu trúc để người dùng đọc nhanh hơn.
- Feedback cho thấy AI thường trả lời chưa tốt vì tài liệu gốc khó đọc.

Không nên tạo wiki khi:

- Tài liệu gốc chưa approved.
- Nội dung còn tranh cãi hoặc chưa có owner xác nhận.
- Tài liệu quá nhạy cảm.
- Tài liệu gốc thiếu thông tin quan trọng và cần chỉnh trước.

Các bước tạo wiki draft:

1. Vào màn hình `Documents`.
2. Chọn tài liệu đã approved.
3. Bấm `Generate wiki draft`.
4. Chờ hệ thống sinh draft.
5. Vào màn hình `Wiki`.
6. Mở draft vừa tạo để kiểm tra.

## 9. Publish Hoặc Reject Wiki

Reviewer kiểm tra wiki draft trước khi publish.

Trước khi publish, cần kiểm tra:

- Nội dung wiki không thêm thông tin ngoài tài liệu gốc.
- Tiêu đề rõ nghĩa.
- Cấu trúc dễ đọc.
- Không thiếu ý quan trọng.
- Visibility và folder đúng phạm vi chia sẻ.
- Nếu publish rộng toàn công ty, nội dung phải được phép chia sẻ rộng.

Các bước publish:

1. Vào màn hình `Wiki`.
2. Chọn wiki draft cần xử lý.
3. Đọc toàn bộ nội dung draft.
4. Kiểm tra tài liệu liên quan và thông tin còn thiếu nếu có.
5. Chọn visibility phù hợp.
6. Bấm `Publish wiki`.
7. Kiểm tra dashboard hoặc hỏi AI lại để xác nhận wiki đã được dùng.

Reject wiki khi:

- Draft sai hoặc thêm thông tin không có trong tài liệu.
- Draft thiếu nội dung quan trọng.
- Cần sửa tài liệu gốc trước.
- Visibility/folder chưa phù hợp.

Khi reject, ghi lý do để người xử lý biết cần sửa gì.

## 10. Quản Lý Folder Và Phân Quyền

Admin hoặc reviewer được cấp quyền quản lý folder cần chuẩn bị folder trước khi user upload.

Nguyên tắc folder:

- Tên folder ngắn và rõ nghĩa.
- Phân theo team hoặc chủ đề công việc.
- Không tạo cấu trúc quá sâu nếu chưa cần.
- Mỗi folder nên có owner hoặc reviewer phụ trách.

Ví dụ:

```text
/Support
/Support/FAQ
/Support/Refund
/Engineering
/Operations
/Onboarding
```

Trước khi user upload:

- Tạo folder đúng phạm vi.
- Cấp quyền xem/upload cho team hoặc user phù hợp.
- Đảm bảo reviewer biết folder nào mình cần duyệt.

## 11. Trạng Thái Thường Gặp

Tài liệu:

- PendingReview: tài liệu hoặc version đang chờ reviewer duyệt.
- Approved: tài liệu đã được duyệt.
- Rejected: tài liệu bị từ chối.
- Indexed: nội dung đã được xử lý và sẵn sàng cho AI nếu user có quyền.

Wiki:

- Draft: wiki đang chờ reviewer kiểm tra.
- Published: wiki đã publish và có thể được AI ưu tiên dùng.
- Rejected: wiki draft bị từ chối.
- Archived: wiki không còn dùng làm nguồn chính.

## 12. Checklist Sử Dụng Nhanh

Trước khi upload:

- File thuộc định dạng hỗ trợ.
- Nội dung còn hiệu lực.
- Không chứa dữ liệu cấm.
- Tên file và tên tài liệu rõ nghĩa.
- Chọn đúng folder.

Trước khi approve tài liệu:

- Đúng folder và đúng phạm vi.
- Nội dung có nguồn hoặc owner rõ.
- Không có dữ liệu nhạy cảm ngoài phạm vi pilot.
- File đọc được.

Trước khi publish wiki:

- Draft đúng với tài liệu gốc.
- Không tự thêm thông tin.
- Có cấu trúc dễ đọc.
- Visibility đúng.
- Có ích cho Q&A.

Khi AI trả lời chưa tốt:

- Kiểm tra citation.
- Gửi feedback `Incorrect` kèm ghi chú.
- Reviewer xem feedback để quyết định bổ sung tài liệu, tạo correction hoặc tạo lại wiki.
