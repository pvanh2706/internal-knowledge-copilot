# Enterprise AI Copilot — Decision Log

**Version:** 0.1  
**Document status:** APPROVED  
**Last updated:** 2026-10-29  
**Owner:** Product Owner

## 1. Purpose

Tài liệu này quản lý các quyết định sản phẩm và kiến trúc chính thức của Enterprise AI Copilot.

GitHub B là nguồn lưu trữ quyết định dài hạn.

Một quyết định chỉ được xem là APPROVED khi Product Owner xác nhận.

## 2. Decision Registry

| ID | Decision | Category | Status |
|---|---|---|---|
| [DEC-0001](DEC-0001.md) | Select Internal Knowledge Copilot B as Primary Product | Product Strategy | APPROVED |
| [DEC-0002](DEC-0002.md) | Enterprise Context-Aware AI Copilot Direction | Product Strategy | APPROVED |
| [DEC-0003](DEC-0003.md) | Source System Ownership and Capability Reuse | Product Architecture | APPROVED |

Các nội dung trên đã được PO thống nhất trong hội thoại; tài liệu được đề xuất đưa vào main qua Pull Request Gate A, chưa đồng nghĩa đã merge.

## 3. Decision Lifecycle

**PROPOSED**
- Đang được xem xét.
- Không được tự động áp dụng như kiến trúc đã duyệt.

**APPROVED**
- Product Owner đã phê duyệt.
- Có hiệu lực trong phạm vi được xác định.
- Không đồng nghĩa đã triển khai code.

**SUPERSEDED**
- Đã được thay thế bằng quyết định mới.
- Phải ghi rõ mã quyết định thay thế.
- Giữ lịch sử để truy vết.

## 4. Decision Record Requirements

Mỗi decision record phải có:

- Decision ID và title.
- Category.
- Status.
- Context/problem.
- Options considered.
- Decision.
- Rationale.
- Consequences/trade-offs.
- Related decisions.
- Approval information.

## 5. Governance Rules

1. Chỉ PO có quyền phê duyệt quyết định.
2. ChatGPT được đề xuất và phản biện.
3. Coding agent không được tự ý thay đổi quyết định đã phê duyệt.
4. Thay đổi quyết định quan trọng phải được review.
5. Khi thay thế quyết định cũ, tạo record mới và đánh dấu record cũ SUPERSEDED.
6. Không xóa lịch sử quyết định.
7. Cập nhật GitHub chỉ khi được PO cho phép.

## 6. Related Documents

- [Project Context](../../PROJECT_CONTEXT.md)
- [Project Status](../../PROJECT_STATUS.md)
- [AI Handoff](../../BÀN_GIAO_AI.md)
- [MVP Roadmap](../TỔNG_QUAN_TRẠNG_THÁI_ROADMAP.md)
