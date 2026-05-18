# Internal Knowledge Copilot - API Spec MVP

Ngày lập: 2026-05-09

## 1. Quy ước chung

Base path:

```text
/api
```

Response lỗi nên có dạng:

```json
{
  "error": "Mã lỗi ngắn",
  "message": "Thông báo lỗi tiếng Việt"
}
```

Tất cả endpoint cần xác thực trừ login.

Role:

- Admin
- User
- Reviewer

## 2. Auth

### POST /api/auth/login

Quyền:

- Public.

Request:

```json
{
  "email": "user@company.com",
  "password": "password"
}
```

Response:

```json
{
  "accessToken": "jwt",
  "mustChangePassword": true,
  "user": {
    "id": "id",
    "displayName": "Tên người dùng",
    "role": "User"
  }
}
```

### POST /api/auth/change-password

Quyền:

- Authenticated.

Request:

```json
{
  "currentPassword": "old",
  "newPassword": "new"
}
```

## 3. Users

### GET /api/users

Quyền:

- Admin.

### POST /api/users

Quyền:

- Admin.

Request:

```json
{
  "email": "user@company.com",
  "displayName": "Nguyễn Văn A",
  "role": "User",
  "primaryTeamId": "team-id",
  "initialPassword": "password"
}
```

### PATCH /api/users/{id}

Quyền:

- Admin.

## 4. Teams

### GET /api/teams

Quyền:

- Admin, Reviewer.

### POST /api/teams

Quyền:

- Admin.

## 5. Folders

### GET /api/folders/tree

Quyền:

- Authenticated.

Ghi chú:

- Chỉ trả folder user có quyền thấy.

### POST /api/folders

Quyền:

- Admin, Reviewer.

### PATCH /api/folders/{id}

Quyền:

- Admin, Reviewer.

### DELETE /api/folders/{id}

Quyền:

- Admin, Reviewer.

Ghi chú:

- Soft delete.

### PUT /api/folders/{id}/permissions

Quyền:

- Admin, Reviewer.

## 6. Documents

### GET /api/documents

Quyền:

- Authenticated.

Query:

- folderId
- status
- keyword

Ghi chú:

- Chỉ trả document user có quyền thấy.

### POST /api/documents

Quyền:

- User, Admin, Reviewer.

Content-Type:

- multipart/form-data

Fields:

- folderId
- title
- description
- file

### POST /api/documents/{id}/versions

Quyền:

- User, Admin, Reviewer.

Ghi chú:

- Upload version mới cho document hiện có.

### GET /api/documents/{id}

Quyền:

- User có quyền xem document.

### GET /api/documents/{id}/download

Quyền:

- User có quyền xem document.

Ghi chú:

- File download phải đi qua API để kiểm tra quyền.

### POST /api/documents/{id}/approve

Quyền:

- Reviewer.

Request:

```json
{
  "versionId": "version-id"
}
```

### POST /api/documents/{id}/reject

Quyền:

- Reviewer.

Request:

```json
{
  "versionId": "version-id",
  "reason": "Lý do reject"
}
```

## 7. AI Q&A

### POST /api/ai/ask

Quyền:

- Authenticated.

Request:

```json
{
  "question": "Quy trình xử lý lỗi thanh toán là gì?",
  "scopeType": "All",
  "folderId": null,
  "documentId": null
}
```

scopeType:

- All
- Folder
- Document

Response:

```json
{
  "interactionId": "id",
  "answer": "Câu trả lời tiếng Việt...",
  "needsClarification": false,
  "citations": [
    {
      "sourceType": "Wiki",
      "title": "Quy trình xử lý lỗi thanh toán",
      "folderPath": "/Support/Payment",
      "excerpt": "Đoạn trích ngắn..."
    }
  ]
}
```

## 8. Feedback

### POST /api/ai/interactions/{id}/feedback

Quyền:

- Authenticated.

Request:

```json
{
  "value": "Incorrect",
  "note": "Câu trả lời thiếu bước kiểm tra log."
}
```

### GET /api/feedback/incorrect

Quyền:

- Reviewer.

### PATCH /api/feedback/{id}/review-status

Quyền:

- Reviewer.

Request:

```json
{
  "status": "Resolved",
  "reviewerNote": "Đã cập nhật tài liệu nguồn."
}
```

## 9. Wiki

### POST /api/wiki/generate

Quyền:

- Reviewer.

Request:

```json
{
  "documentId": "document-id",
  "documentVersionId": "version-id"
}
```

### GET /api/wiki/drafts

Quyền:

- Reviewer.

### GET /api/wiki/drafts/{id}

Quyền:

- Reviewer.

### POST /api/wiki/drafts/{id}/publish

Quyền:

- Reviewer.

Request:

```json
{
  "visibilityScope": "Folder",
  "folderId": "folder-id",
  "isCompanyPublicConfirmed": false
}
```

Nếu visibilityScope = Company:

- isCompanyPublicConfirmed phải là true.

### POST /api/wiki/drafts/{id}/reject

Quyền:

- Reviewer.

Request:

```json
{
  "reason": "Lý do reject"
}
```

## 10. Dashboard

### GET /api/dashboard/summary

Quyền:

- Admin, Reviewer.

Query:

- from
- to
- teamId
- folderId

Response gồm:

- document counts by status
- wiki counts by status
- ai question count
- feedback correct/incorrect rate
- incorrect feedback pending count
- top cited sources

## 11. Audit Logs

### GET /api/audit-logs

Quyền:

- Admin.

Query:

- from
- to
- action
- entityType

