# So Sánh Có Và Không Có Wiki Trong Internal Knowledge Copilot

Tài liệu này dùng để giải thích rõ tác dụng của wiki trong Internal Knowledge Copilot. Mục tiêu là giúp team, reviewer hoặc người xem demo thấy được khác biệt giữa hai trạng thái:

- Chỉ có document đã approve/index.
- Có thêm wiki đã được reviewer publish từ document đó.

## 1. Kết Luận Ngắn

Không có wiki, hệ thống vẫn có thể trả lời dựa trên document đã approve và indexed. Tuy nhiên chất lượng câu trả lời phụ thuộc nhiều vào chất lượng file gốc: file dài, format rối, nhiều đoạn lặp hoặc thông tin nằm rải rác thì AI dễ trả lời dài, thiếu trọng tâm hoặc cần hỏi lại.

Có wiki, hệ thống có thêm một lớp tri thức đã được chuẩn hóa. Wiki được sinh từ document đã indexed, được reviewer kiểm tra, publish và index riêng. Khi hỏi AI, wiki published được ưu tiên hơn document gốc, nên câu trả lời thường ngắn hơn, rõ hơn, dễ cite hơn và ổn định hơn cho các câu hỏi lặp lại.

## 2. Hai Luồng Tri Thức

### Không Có Wiki

```text
Upload document
  -> Reviewer approve
  -> Worker extract/chunk/embed
  -> Document version = Indexed
  -> User hỏi AI
  -> Retrieval lấy document chunks
  -> AI trả lời dựa trên document chunks
```

Đặc điểm:

- Document là nguồn chính.
- Chunk có `source_type = document`.
- Nội dung vẫn giữ cấu trúc và mức độ rõ/rối của file gốc.
- Nếu thông tin nằm ở nhiều đoạn xa nhau, retrieval có thể lấy thiếu ngữ cảnh.

### Có Wiki

```text
Upload document
  -> Reviewer approve
  -> Worker extract/chunk/embed
  -> Document version = Indexed
  -> Reviewer generate wiki draft
  -> Reviewer kiểm tra và publish wiki
  -> Wiki được chunk/embed/index
  -> User hỏi AI
  -> Retrieval ưu tiên wiki published
  -> AI trả lời dựa trên wiki, fallback document khi cần
```

Đặc điểm:

- Wiki là lớp tri thức chuẩn hóa từ document gốc.
- Chunk có `source_type = wiki`.
- Wiki chỉ dùng được sau khi reviewer publish.
- Document vẫn tồn tại làm nguồn gốc và fallback.

## 3. Bảng So Sánh Tác Dụng

| Tiêu chí | Không có wiki | Có wiki published | Tác dụng nhìn thấy |
|---|---|---|---|
| Nguồn AI ưu tiên | Document chunks | Wiki chunks trước, document fallback | Câu trả lời dựa trên nguồn đã chuẩn hóa hơn |
| Độ ngắn gọn | Phụ thuộc file gốc | Thường ngắn và tập trung hơn | User đọc nhanh hơn |
| Độ ổn định | Dễ thay đổi theo chunk retrieval | Ổn định hơn nếu wiki được viết rõ | Cùng câu hỏi lặp lại dễ ra câu trả lời nhất quán |
| Câu hỏi quy trình | Có thể phải ghép nhiều đoạn trong document | Wiki có thể gom mục đích, phạm vi, bước xử lý, FAQ | Trả lời đúng trọng tâm hơn |
| Tài liệu dài | Dễ lấy thiếu hoặc thừa context | Wiki tóm tắt phần quan trọng | Giảm nhiễu từ file gốc |
| Tài liệu format xấu | AI phải xử lý nhiều đoạn rối | Reviewer đã có cơ hội chỉnh draft trước publish | Giảm ảnh hưởng của format gốc |
| Citation | Thường cite document gốc | Thường cite wiki, có thể cite document khi cần | Nguồn tham khảo dễ đọc hơn |
| Kiểm soát chất lượng | Dừng ở approve document | Có thêm bước reviewer publish wiki | Tri thức chính thức hơn |
| Bảo trì | Cập nhật bằng version document mới | Cần tạo/publish lại wiki khi nội dung đổi lớn | Có lớp tri thức sạch nhưng cần quản trị |
| Phù hợp demo | Chứng minh RAG đọc tài liệu | Chứng minh RAG có tri thức đã biên tập | Dễ thấy giá trị sản phẩm hơn |

## 4. Ví Dụ Minh Họa Khi Demo

Giả sử có document gốc:

```text
Tên document: Quy trình thanh toán nội bộ
Đặc điểm: 15-20 trang, có nhiều mục, có bảng hạn mức duyệt, có FAQ cuối tài liệu.
```

Câu hỏi demo:

```text
Thanh toán dưới 5 triệu cần ai duyệt?
```

### Trước Khi Có Wiki

Kết quả mong đợi:

- AI tìm trong document chunks.
- Citation thường là document gốc.
- Câu trả lời có thể dài hơn vì context lấy từ nhiều đoạn.
- Nếu document ghi rải rác, AI có thể trả `confidence = medium/low` hoặc báo thiếu thông tin.

Điều cần quan sát:

- Citation source type là `Document`.
- Retrieval explain có final context chủ yếu từ document.
- Câu trả lời phụ thuộc mạnh vào đoạn chunk được chọn.

### Sau Khi Có Wiki

Reviewer generate wiki draft từ document, kiểm tra lại nội dung, rồi publish wiki. Sau đó hỏi lại cùng câu hỏi.

Kết quả mong đợi:

- AI ưu tiên wiki published nếu wiki có mục hạn mức duyệt.
- Citation thường là `Wiki`.
- Câu trả lời ngắn hơn, ví dụ tập trung vào hạn mức, người duyệt, điều kiện áp dụng.
- Nếu wiki thiếu chi tiết, AI vẫn có thể fallback sang document gốc.

Điều cần quan sát:

- Citation source type chuyển từ `Document` sang `Wiki` hoặc có `Wiki` đứng trước.
- Retrieval explain cho thấy wiki được chọn vào final context.
- Câu trả lời ít nhiễu hơn và dễ dùng trực tiếp hơn.

## 5. Cách Đo Hiệu Quả Có/Không Có Wiki

Nên đo cùng một bộ câu hỏi trước và sau khi publish wiki.

| Chỉ số | Cách đo | Kỳ vọng khi có wiki |
|---|---|---|
| Tỷ lệ câu trả lời dùng wiki | Xem citation hoặc `UsedWikiCount` trong interaction | Tăng với các câu hỏi thuộc chủ đề đã publish wiki |
| Số câu cần hỏi lại | Xem `NeedsClarification` | Giảm nếu wiki đủ rõ |
| Độ tự tin | Xem `Confidence` | Tăng từ low/medium lên medium/high trong câu hỏi phổ biến |
| Độ dài câu trả lời | So sánh câu trả lời cùng câu hỏi | Ngắn hơn nhưng vẫn đủ ý |
| Feedback đúng/sai | User gửi feedback sau câu trả lời | Tỷ lệ đúng tăng |
| Thời gian reviewer xử lý lỗi tri thức | Theo dõi feedback sai cần sửa | Giảm vì wiki gom tri thức chuẩn |

## 6. Kịch Bản Demo Nhanh

1. Upload và approve một document dài có quy trình rõ.
2. Chờ document version chuyển sang `Indexed`.
3. Hỏi 2-3 câu phổ biến khi chưa có wiki.
4. Ghi lại câu trả lời, citation và confidence.
5. Generate wiki draft từ document đã indexed.
6. Reviewer đọc, chỉnh nếu cần, rồi publish wiki.
7. Hỏi lại đúng 2-3 câu ở bước 3.
8. So sánh:
   - Citation dùng `Document` hay `Wiki`.
   - Câu trả lời có ngắn và đúng trọng tâm hơn không.
   - Có giảm `NeedsClarification` không.
   - Retrieval explain có đưa wiki vào final context không.

## 7. Khi Nào Nên Tạo Wiki

Nên tạo wiki cho:

- Tài liệu dài nhưng hay được hỏi.
- Quy trình nội bộ có nhiều bước.
- Chính sách có hạn mức, điều kiện, ngoại lệ.
- FAQ, onboarding, hướng dẫn xử lý sự cố.
- Nội dung cần chia sẻ rộng cho nhiều team.
- Tài liệu gốc format không đồng nhất nhưng thông tin quan trọng.

Chưa nên tạo wiki cho:

- Tài liệu chưa được approve hoặc còn tranh cãi.
- Tài liệu rất ngắn, đã rõ và ít được hỏi.
- Tài liệu nhạy cảm chưa xác định đúng visibility.
- Tài liệu nguồn sai hoặc lỗi thời.
- Nội dung cần chuyên gia biên tập trước khi đưa vào tri thức chính thức.

## 8. Rủi Ro Và Giới Hạn

Wiki không thay thế hoàn toàn document gốc. Wiki là lớp tri thức đã biên tập để phục vụ đọc nhanh và hỏi đáp tốt hơn.

Cần lưu ý:

- Wiki phải được reviewer kiểm tra trước khi publish.
- Wiki không được thêm thông tin ngoài document gốc nếu chưa có nguồn xác thực.
- Khi document đổi lớn, reviewer nên tạo lại hoặc cập nhật wiki.
- Nếu publish company-wide, reviewer phải chắc chắn nội dung được phép chia sẻ rộng.
- Nếu wiki viết sai, AI có thể ưu tiên nguồn sai đó, nên governance của reviewer rất quan trọng.

## 9. Thông Điệp Trình Bày Với Sếp Hoặc Team

Thông điệp ngắn:

```text
Document giúp hệ thống có dữ liệu để đọc.
Wiki giúp biến dữ liệu đó thành tri thức đã chuẩn hóa.
Không có wiki, AI vẫn hỏi đáp được nhưng phụ thuộc nhiều vào file gốc.
Có wiki, AI có nguồn ưu tiên rõ hơn, ngắn hơn và đã được reviewer kiểm soát.
```

Thông điệp khi demo:

```text
Điểm khác biệt không phải là có thêm một file tóm tắt.
Điểm khác biệt là wiki trở thành nguồn tri thức chính thức được index, được ưu tiên trong retrieval và vẫn nằm trong mô hình phân quyền/citation của hệ thống.
```

## 10. Checklist Chứng Minh Tác Dụng Wiki

- Có ít nhất một document dài đã `Indexed`.
- Có một bộ câu hỏi dùng lại trước và sau khi publish wiki.
- Có kết quả trước wiki: answer, confidence, citation.
- Có wiki draft đã được reviewer kiểm tra.
- Có wiki page đã `Published`.
- Có kết quả sau wiki: answer, confidence, citation.
- Có ảnh chụp hoặc ghi chú so sánh source type `Document` và `Wiki`.
- Có nhận xét của reviewer/user về câu trả lời sau khi có wiki.

## 11. Kết Luận

Wiki có tác dụng rõ nhất ở các tài liệu dài, hay được hỏi và cần câu trả lời nhất quán. Trong Internal Knowledge Copilot, wiki không chỉ là nội dung phụ để đọc thủ công. Wiki là một nguồn tri thức có lifecycle riêng: generate draft, reviewer publish, index vào vector DB và được ưu tiên trong luồng AI Q&A.

Vì vậy, khi đánh giá hiệu quả hệ thống, nên so sánh cùng một câu hỏi ở hai trạng thái:

1. Chỉ có document indexed.
2. Có thêm wiki published từ document đó.

Nếu wiki được viết và publish đúng, người dùng sẽ thấy câu trả lời rõ hơn, citation dễ đọc hơn và reviewer có nhiều quyền kiểm soát chất lượng tri thức hơn.
