# Hướng Dẫn Upload File Demo Để So Sánh Có Và Không Có Wiki

Tài liệu này hướng dẫn cách dùng file demo để thấy sự khác biệt trước và sau khi publish wiki trong Internal Knowledge Copilot.

Không upload file hướng dẫn này vào phần mềm. Chỉ upload file sau:

```text
docs/demo-upload/QUY_TRINH_THANH_TOAN_NOI_BO_DEMO_WIKI.md
```

## 1. Mục Tiêu Demo

Bạn sẽ kiểm tra cùng một bộ câu hỏi ở hai trạng thái:

1. Chỉ có document đã approve và indexed.
2. Có thêm wiki đã được generate, reviewer kiểm tra và publish.

Điểm cần quan sát:

- Trước khi có wiki, citation thường là `Document`.
- Sau khi có wiki, citation thường chuyển sang `Wiki` hoặc wiki đứng trước document.
- Câu trả lời sau khi có wiki thường ngắn hơn, tập trung hơn và dễ dùng hơn.

Nếu đang dùng mock AI local, khác biệt lớn nhất sẽ nằm ở citation/source type và retrieval explain. Nếu dùng provider AI thật, khác biệt về cách diễn đạt câu trả lời sẽ rõ hơn.

## 2. Cách Upload Và Chuẩn Bị

1. Vào màn hình Documents.
2. Upload file `docs/demo-upload/QUY_TRINH_THANH_TOAN_NOI_BO_DEMO_WIKI.md`.
3. Đặt title gợi ý: `Demo - Quy trình thanh toán nội bộ`.
4. Chọn folder mà user demo có quyền xem.
5. Reviewer/Admin approve document.
6. Chờ document version chuyển sang `Indexed`.

Sau bước này, chưa tạo wiki. Hãy hỏi AI trước để có kết quả baseline.

## 3. Câu Hỏi Nên Hỏi Trước Khi Có Wiki

Hỏi lần lượt các câu sau và ghi lại câu trả lời, confidence và citation:

```text
Thanh toán dưới 5 triệu cần ai duyệt?
```

```text
Hồ sơ thanh toán cần những giấy tờ nào?
```

```text
Nếu hồ sơ thiếu mã ngân sách thì xử lý thế nào?
```

```text
Thanh toán khẩn cấp trong ngày cần điều kiện gì?
```

```text
Vendor đổi tài khoản ngân hàng thì phải kiểm tra gì?
```

Kỳ vọng trước khi có wiki:

- Citation source type chủ yếu là `Document`.
- Câu trả lời có thể dài hoặc trích nhiều đoạn từ tài liệu gốc.
- Retrieval explain, nếu mở, sẽ cho thấy final context lấy từ document chunks.

## 4. Tạo Và Publish Wiki

1. Vào document đã upload.
2. Chọn tạo wiki draft từ document đã `Indexed`.
3. Reviewer đọc lại draft.
4. Nếu draft ổn, publish wiki theo folder hoặc company tùy nhu cầu demo.
5. Đảm bảo wiki đã được publish và index vào vector DB.

Nếu draft có đoạn sai hoặc thiếu, chỉnh theo nội dung document gốc trước khi publish.

## 5. Hỏi Lại Sau Khi Có Wiki

Hỏi lại đúng các câu ở bước 3:

```text
Thanh toán dưới 5 triệu cần ai duyệt?
Hồ sơ thanh toán cần những giấy tờ nào?
Nếu hồ sơ thiếu mã ngân sách thì xử lý thế nào?
Thanh toán khẩn cấp trong ngày cần điều kiện gì?
Vendor đổi tài khoản ngân hàng thì phải kiểm tra gì?
```

Kỳ vọng sau khi có wiki:

- Citation có `Wiki` hoặc wiki đứng trước document.
- Câu trả lời ngắn hơn và trực tiếp hơn.
- Các câu hỏi về hạn mức, hồ sơ bắt buộc, cutoff 15:00, thanh toán khẩn cấp và vendor đổi tài khoản nên dễ trả lời hơn.

## 6. Bảng Ghi Kết Quả

| Câu hỏi | Trước wiki: source type | Trước wiki: confidence | Sau wiki: source type | Sau wiki: confidence | Nhận xét |
|---|---|---|---|---|---|
| Thanh toán dưới 5 triệu cần ai duyệt? |  |  |  |  |  |
| Hồ sơ thanh toán cần những giấy tờ nào? |  |  |  |  |  |
| Nếu hồ sơ thiếu mã ngân sách thì xử lý thế nào? |  |  |  |  |  |
| Thanh toán khẩn cấp trong ngày cần điều kiện gì? |  |  |  |  |  |
| Vendor đổi tài khoản ngân hàng thì phải kiểm tra gì? |  |  |  |  |  |

## 7. Cách Giải Thích Khi Demo

Thông điệp ngắn:

```text
Khi chưa có wiki, AI đọc từ document gốc đã index.
Khi có wiki, hệ thống có thêm nguồn tri thức đã được reviewer chuẩn hóa và publish.
Vì wiki được ưu tiên trong retrieval, câu trả lời thường rõ hơn và citation dễ đọc hơn.
```

Điểm quan trọng:

- Wiki không thay thế document gốc.
- Wiki là lớp tri thức chính thức đã qua reviewer.
- Document vẫn là nguồn gốc và fallback.
- Nếu document thay đổi lớn, reviewer nên tạo lại hoặc cập nhật wiki.
