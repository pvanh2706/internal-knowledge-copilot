# Hướng Dẫn Tích Hợp Bên Thứ 3

Ngày cập nhật: 2026-06-05

Tài liệu này hướng dẫn một hệ thống bên thứ 3 tích hợp với Internal Knowledge Copilot để:

- Đồng bộ tài liệu, quy trình, object nghiệp vụ và quyền truy cập.
- Cho người dùng hỏi AI trên nguồn tri thức được phép xem.
- Nhận gợi ý workflow có citation khi có ngữ cảnh nghiệp vụ.
- Chuẩn bị checklist trước khi go-live.

## 1. Trạng Thái Triển Khai Hiện Tại

### Đã chạy được

- Upload tài liệu qua API nội bộ `POST /api/documents`.
- Reviewer/Admin duyệt tài liệu qua `POST /api/documents/{id}/approve`.
- Background job xử lý tài liệu đã duyệt: extract text, chunk, embedding, index.
- Hỏi AI qua `POST /api/ai/ask`, có lọc theo tenant, folder/document và citation.
- Nhận integration inbound event:
  - `POST /api/integrations/{applicationCode}/events`
  - `POST /api/integrations/{applicationCode}/documents/changed`
  - `POST /api/integrations/{applicationCode}/objects/sync`
  - `POST /api/integrations/{applicationCode}/permissions/sync`
- Sync external object metadata và ACL snapshot qua background job.
- Lọc quyền cho external candidates bằng ACL snapshot và connector revalidation.
- Workflow recommendation cho case CRM deal stage changed qua `POST /api/workflow-copilot/deal-stage-changed`.

### Cần hoàn thiện trước khi coi là partner integration hoàn chỉnh

- `documents/changed` hiện nhận và lưu event, nhưng chưa tự kéo content từ bên thứ 3 rồi index thành knowledge chunk.
- Chưa có endpoint generic "AI review tài liệu quy trình" như `POST /api/process-review`.
- Chưa có connector marketplace cho từng sản phẩm bên thứ 3.
- API spec MVP chưa cập nhật đầy đủ các endpoint integration mới.

Vì vậy, với hệ thống hiện tại, luồng ổn định nhất cho tài liệu là:

```text
Bên thứ 3 hoặc operator upload file qua /api/documents
-> Reviewer/Admin approve
-> Copilot index
-> User hỏi AI theo quyền
```

Luồng integration object/permission đã có contract và API, phù hợp để nối dần với hệ thống gốc:

```text
Bên thứ 3 sync object metadata + ACL
-> Copilot lưu external_objects + external_acl_snapshots
-> User hỏi AI hoặc nhận workflow recommendation
-> Copilot lọc quyền bằng ACL snapshot và revalidate khi cần
```

## 2. Khái Niệm Chính

| Khái niệm | Ý nghĩa |
| --- | --- |
| Tenant | Ranh giới dữ liệu của một khách hàng/đơn vị. Mọi request nên có `X-Tenant-Code`. |
| Application | Hệ thống bên thứ 3 được tích hợp, ví dụ CRM, Sales, Ticketing, Wiki Portal. |
| Integration connection | Cấu hình kết nối và secret cho một application. |
| Knowledge source | Nguồn tri thức local hoặc external. |
| External object | Object từ hệ thống gốc, ví dụ document, deal, ticket, procedure. |
| ACL snapshot | Bản chụp quyền từ hệ thống gốc để Copilot lọc retrieval nhanh. |
| Revalidation | Copilot gọi lại hệ thống gốc để kiểm tra quyền thật trước khi dùng dữ liệu nhạy cảm. |
| Idempotency key | Khóa chống xử lý trùng khi bên thứ 3 gửi lại event. |

## 3. Base URL Và Headers

Ví dụ local:

```text
http://localhost:5000/api
```

Ví dụ production:

```text
https://copilot.company.vn/api
```

### Headers dùng chung

| Header | Bắt buộc | Dùng cho | Ghi chú |
| --- | --- | --- | --- |
| `X-Tenant-Code` | Nên có | Tất cả API | Ví dụ `default`, `acme`. Nếu JWT đã có tenant claim thì header phải khớp. |
| `Content-Type: application/json` | Có với JSON | JSON API | Không set khi upload `multipart/form-data` bằng browser/curl `-F`. |
| `Authorization: Bearer <jwt>` | Có | API user/admin/reviewer | Dùng cho documents, AI Q&A, workflow UI/API. |
| `X-Integration-Key-Id` | Có với integration key | Inbound integration API | Giá trị `secretReference` của integration connection. |
| `X-Integration-Key` | Có với `InternalApiKey` | Inbound integration API | Secret thực tế, chỉ gửi qua HTTPS. |

### Auth model

Có 2 kiểu auth:

1. User JWT

   Dùng khi bên thứ 3 gọi thay mặt user hoặc khi operator/admin thao tác:

   - `POST /api/auth/login`
   - `POST /api/documents`
   - `POST /api/documents/{id}/approve`
   - `POST /api/ai/ask`
   - `POST /api/workflow-copilot/deal-stage-changed`

2. Integration API key

   Dùng cho hệ thống bên thứ 3 đẩy event/sync vào Copilot:

   - `POST /api/integrations/{applicationCode}/events`
   - `POST /api/integrations/{applicationCode}/documents/changed`
   - `POST /api/integrations/{applicationCode}/objects/sync`
   - `POST /api/integrations/{applicationCode}/permissions/sync`

## 4. Chuẩn Bị Tích Hợp

### Bước 1. Tạo tenant và application

Tạo tenant/application trên UI Admin hoặc API quản trị hiện có. Ghi lại:

- `tenantCode`, ví dụ `acme`.
- `applicationCode`, ví dụ `crm`.
- `applicationId`, dùng cho một số API như AI scope hoặc workflow.

### Bước 2. Tạo integration connection

Admin tạo connection:

```http
POST /api/integrations/connections
Authorization: Bearer <admin_jwt>
X-Tenant-Code: acme
Content-Type: application/json
```

Payload:

```json
{
  "applicationId": "11111111-1111-1111-1111-111111111111",
  "name": "CRM production connector",
  "baseUrl": "https://crm.example.com",
  "authMode": "InternalApiKey",
  "secretReference": "crm-prod-key-2026-06",
  "secretValue": "replace-with-a-long-random-secret",
  "status": "Active",
  "metadataJson": "{\"owner\":\"crm-team\",\"environment\":\"production\"}"
}
```

Response trả về `secretConfigured = true`. Từ thời điểm này, bên thứ 3 gọi inbound integration API bằng:

```text
X-Integration-Key-Id: crm-prod-key-2026-06
X-Integration-Key: replace-with-a-long-random-secret
```

### Bước 3. Chuẩn hóa định danh user và group

Hai bên cần thống nhất:

- `SubjectType`: nên dùng `user`, `team`, `group`, hoặc `role`.
- `SubjectId`: ID ổn định từ hệ thống gốc, ví dụ `user-123`, `sales-team`.
- Mapping user Copilot với subject bên thứ 3.

Nếu mapping user không ổn định, AI có thể lọc sai quyền.

## 5. Luồng Upload Tài Liệu Quy Trình Đang Chạy Được

Luồng này dùng API nội bộ của Copilot và JWT.

```text
1. Bên thứ 3/operator lấy JWT.
2. Upload file quy trình vào folder đã phân quyền.
3. Reviewer/Admin approve version.
4. Worker xử lý DocumentSync.
5. User hỏi AI trên tài liệu đã index.
```

### 5.1 Đăng nhập lấy JWT

```bash
curl -X POST "http://localhost:5000/api/auth/login" \
  -H "X-Tenant-Code: acme" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "reviewer@example.local",
    "password": "P@ssw0rd!"
  }'
```

### 5.2 Upload tài liệu

```bash
curl -X POST "http://localhost:5000/api/documents" \
  -H "X-Tenant-Code: acme" \
  -H "Authorization: Bearer <jwt>" \
  -F "folderId=22222222-2222-2222-2222-222222222222" \
  -F "title=Quy trình thanh toán nhà cung cấp" \
  -F "description=Quy trình do hệ thống CRM đẩy lên" \
  -F "file=@./QUY_TRINH_THANH_TOAN.md;type=text/markdown"
```

File hỗ trợ theo UI hiện tại:

- `.pdf`
- `.docx`
- `.md`
- `.markdown`
- `.txt`

Response có `id` của document và `versions[0].id`.

### 5.3 Approve để index

```bash
curl -X POST "http://localhost:5000/api/documents/<documentId>/approve" \
  -H "X-Tenant-Code: acme" \
  -H "Authorization: Bearer <reviewer_or_admin_jwt>" \
  -H "Content-Type: application/json" \
  -d '{
    "versionId": "33333333-3333-3333-3333-333333333333"
  }'
```

Sau approve, worker sẽ xử lý. Kiểm tra trạng thái bằng:

```bash
curl "http://localhost:5000/api/documents/<documentId>" \
  -H "X-Tenant-Code: acme" \
  -H "Authorization: Bearer <jwt>"
```

Khi version có `status = Indexed`, tài liệu có thể dùng cho AI.

## 6. Luồng Sync Metadata Tài Liệu/Object Từ Bên Thứ 3

Luồng này dùng integration API key. Phù hợp khi hệ thống gốc muốn báo cho Copilot biết có object/tài liệu mới hoặc thay đổi.

### 6.1 Báo tài liệu thay đổi

Endpoint:

```text
POST /api/integrations/{applicationCode}/documents/changed
```

Payload:

```json
{
  "idempotencyKey": "doc-proc-pay-001:v3",
  "externalDocumentId": "proc-pay-001",
  "changeType": "updated",
  "knowledgeSourceExternalId": "crm-procedure-library",
  "title": "Quy trình thanh toán nhà cung cấp",
  "url": "https://crm.example.com/procedures/proc-pay-001",
  "contentHash": "sha256:9d79b1...",
  "changedAt": "2026-06-05T03:15:00Z",
  "metadataJson": "{\"department\":\"Finance\",\"version\":\"3\"}"
}
```

curl:

```bash
curl -X POST "http://localhost:5000/api/integrations/crm/documents/changed" \
  -H "X-Tenant-Code: acme" \
  -H "X-Integration-Key-Id: crm-prod-key-2026-06" \
  -H "X-Integration-Key: <integration_secret>" \
  -H "Content-Type: application/json" \
  -d '{
    "idempotencyKey": "doc-proc-pay-001:v3",
    "externalDocumentId": "proc-pay-001",
    "changeType": "updated",
    "knowledgeSourceExternalId": "crm-procedure-library",
    "title": "Quy trình thanh toán nhà cung cấp",
    "url": "https://crm.example.com/procedures/proc-pay-001",
    "contentHash": "sha256:9d79b1",
    "changedAt": "2026-06-05T03:15:00Z",
    "metadataJson": "{\"department\":\"Finance\",\"version\":\"3\"}"
  }'
```

Lưu ý hiện tại: endpoint này nhận event và lưu durable inbound event, nhưng chưa tự kéo content/index tài liệu. Nếu cần index ngay, dùng luồng upload ở mục 5 hoặc bổ sung worker xử lý `DocumentChanged`.

### 6.2 Sync external object

Endpoint:

```text
POST /api/integrations/{applicationCode}/objects/sync
```

Payload:

```json
{
  "idempotencyKey": "object-procedure-proc-pay-001:v3",
  "objectType": "procedure",
  "externalObjectId": "proc-pay-001",
  "title": "Quy trình thanh toán nhà cung cấp",
  "knowledgeSourceExternalId": "crm-procedure-library",
  "url": "https://crm.example.com/procedures/proc-pay-001",
  "contentHash": "sha256:9d79b1...",
  "aclHash": "sha256:acl-4a51...",
  "syncedAt": "2026-06-05T03:16:00Z",
  "metadataJson": "{\"owner\":\"finance\",\"status\":\"active\"}"
}
```

curl:

```bash
curl -X POST "http://localhost:5000/api/integrations/crm/objects/sync" \
  -H "X-Tenant-Code: acme" \
  -H "X-Integration-Key-Id: crm-prod-key-2026-06" \
  -H "X-Integration-Key: <integration_secret>" \
  -H "Content-Type: application/json" \
  -d '{
    "idempotencyKey": "object-procedure-proc-pay-001:v3",
    "objectType": "procedure",
    "externalObjectId": "proc-pay-001",
    "title": "Quy trình thanh toán nhà cung cấp",
    "knowledgeSourceExternalId": "crm-procedure-library",
    "url": "https://crm.example.com/procedures/proc-pay-001",
    "contentHash": "sha256:9d79b1",
    "aclHash": "sha256:acl-4a51",
    "syncedAt": "2026-06-05T03:16:00Z",
    "metadataJson": "{\"owner\":\"finance\",\"status\":\"active\"}"
  }'
```

Response mẫu:

```json
{
  "id": "44444444-4444-4444-4444-444444444444",
  "tenantId": "55555555-5555-5555-5555-555555555555",
  "applicationId": "11111111-1111-1111-1111-111111111111",
  "applicationCode": "crm",
  "integrationConnectionId": "66666666-6666-6666-6666-666666666666",
  "eventType": "ObjectSync",
  "idempotencyKey": "object-procedure-proc-pay-001:v3",
  "externalEventId": "proc-pay-001",
  "objectType": "procedure",
  "externalObjectId": "proc-pay-001",
  "status": "Received",
  "receivedAt": "2026-06-05T03:16:01Z",
  "isDuplicate": false
}
```

Nếu gửi lại cùng `idempotencyKey`, API trả `isDuplicate = true` và không tạo event mới.

## 7. Luồng Sync Quyền

Endpoint:

```text
POST /api/integrations/{applicationCode}/permissions/sync
```

Permission hỗ trợ:

- `View`
- `Edit`
- `Owner`

Payload:

```json
{
  "idempotencyKey": "acl-procedure-proc-pay-001:v3",
  "objectType": "procedure",
  "externalObjectId": "proc-pay-001",
  "aclSnapshots": [
    {
      "subjectType": "user",
      "subjectId": "user-123",
      "subjectDisplayName": "Nguyễn Văn A",
      "permission": "View",
      "validFrom": "2026-06-01T00:00:00Z",
      "validTo": null,
      "metadataJson": "{\"source\":\"crm_acl\"}"
    },
    {
      "subjectType": "group",
      "subjectId": "finance-reviewers",
      "subjectDisplayName": "Finance Reviewers",
      "permission": "Owner",
      "validFrom": "2026-06-01T00:00:00Z",
      "validTo": null,
      "metadataJson": "{\"source\":\"crm_acl\"}"
    }
  ],
  "syncedAt": "2026-06-05T03:17:00Z",
  "metadataJson": "{\"aclVersion\":\"3\"}"
}
```

curl:

```bash
curl -X POST "http://localhost:5000/api/integrations/crm/permissions/sync" \
  -H "X-Tenant-Code: acme" \
  -H "X-Integration-Key-Id: crm-prod-key-2026-06" \
  -H "X-Integration-Key: <integration_secret>" \
  -H "Content-Type: application/json" \
  -d '{
    "idempotencyKey": "acl-procedure-proc-pay-001:v3",
    "objectType": "procedure",
    "externalObjectId": "proc-pay-001",
    "aclSnapshots": [
      {
        "subjectType": "user",
        "subjectId": "user-123",
        "subjectDisplayName": "Nguyen Van A",
        "permission": "View",
        "validFrom": "2026-06-01T00:00:00Z",
        "validTo": null,
        "metadataJson": "{\"source\":\"crm_acl\"}"
      }
    ],
    "syncedAt": "2026-06-05T03:17:00Z",
    "metadataJson": "{\"aclVersion\":\"3\"}"
  }'
```

Quy tắc quan trọng:

- Mỗi `(subjectType, subjectId, permission)` chỉ xuất hiện một lần trong cùng request.
- `objectType` được normalize lowercase.
- Khi sync ACL cho object, Copilot thay thế snapshot hiện có của object đó bằng danh sách mới.
- Hệ thống gốc vẫn là source of truth. Với dữ liệu nhạy cảm, nên bật revalidation qua connector.

## 8. API Bên Thứ 3 Nên Cung Cấp Cho Copilot

Nếu muốn tích hợp sâu, hệ thống gốc nên cung cấp các endpoint sau ở `baseUrl` đã cấu hình trong integration connection:

```text
GET  /copilot/documents/{externalId}/content
GET  /copilot/objects/{type}/{externalId}/context
POST /copilot/permissions/check
POST /copilot/actions/validate
POST /copilot/actions/execute
```

### 8.1 Lấy content tài liệu

```http
GET /copilot/documents/proc-pay-001/content
X-Integration-Key-Id: crm-prod-key-2026-06
X-Integration-Key: <integration_secret>
```

Response đề xuất:

```json
{
  "externalId": "proc-pay-001",
  "content": "# Quy trình thanh toán\n\n1. Tiếp nhận hóa đơn...",
  "contentType": "text/markdown",
  "contentHash": "sha256:9d79b1...",
  "metadataJson": "{\"language\":\"vi\",\"documentType\":\"procedure\"}"
}
```

### 8.2 Kiểm tra quyền realtime

```http
POST /copilot/permissions/check
X-Integration-Key-Id: crm-prod-key-2026-06
X-Integration-Key: <integration_secret>
Content-Type: application/json
```

Request:

```json
{
  "objectType": "procedure",
  "externalObjectId": "proc-pay-001",
  "subjectType": "user",
  "subjectId": "user-123",
  "permission": "View"
}
```

Response:

```json
{
  "isAllowed": true,
  "reason": "User has view permission through finance group.",
  "checkedAt": "2026-06-05T03:18:00Z"
}
```

### 8.3 Lấy context object nghiệp vụ

```http
GET /copilot/objects/deal/D-10001/context
X-Integration-Key-Id: crm-prod-key-2026-06
X-Integration-Key: <integration_secret>
```

Response:

```json
{
  "objectType": "deal",
  "externalObjectId": "D-10001",
  "contextJson": "{\"stage\":\"Proposal\",\"amount\":120000000,\"recentActivities\":[\"Customer asked for payment terms\"]}",
  "metadataJson": "{\"source\":\"crm\"}"
}
```

## 9. Hỏi AI Theo Quyền

Endpoint:

```text
POST /api/ai/ask
```

Auth:

- Dùng JWT của user.
- Header `X-Tenant-Code` phải khớp tenant của token nếu token có tenant claim.

### 9.1 Hỏi toàn bộ nguồn được phép xem

```bash
curl -X POST "http://localhost:5000/api/ai/ask" \
  -H "X-Tenant-Code: acme" \
  -H "Authorization: Bearer <user_jwt>" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Quy trình thanh toán nhà cung cấp gồm các bước nào?",
    "scopeType": "All",
    "folderId": null,
    "documentId": null
  }'
```

### 9.2 Hỏi trong một document đã index

```bash
curl -X POST "http://localhost:5000/api/ai/ask" \
  -H "X-Tenant-Code: acme" \
  -H "Authorization: Bearer <user_jwt>" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Khi hồ sơ thiếu hóa đơn thì xử lý thế nào?",
    "scopeType": "Document",
    "folderId": null,
    "documentId": "77777777-7777-7777-7777-777777777777"
  }'
```

### 9.3 Hỏi theo application/external object

Request model đã hỗ trợ các trường:

- `applicationId`
- `knowledgeSourceId`
- `externalObjectType`
- `externalObjectId`

Ví dụ:

```bash
curl -X POST "http://localhost:5000/api/ai/ask" \
  -H "X-Tenant-Code: acme" \
  -H "Authorization: Bearer <user_jwt>" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Dựa trên quy trình liên quan, bước tiếp theo cho hồ sơ này là gì?",
    "scopeType": "All",
    "folderId": null,
    "documentId": null,
    "applicationId": "11111111-1111-1111-1111-111111111111",
    "knowledgeSourceId": null,
    "externalObjectType": "procedure",
    "externalObjectId": "proc-pay-001"
  }'
```

Response mẫu:

```json
{
  "interactionId": "88888888-8888-8888-8888-888888888888",
  "answer": "Quy trình gồm 4 bước chính: tiếp nhận hóa đơn, kiểm tra chứng từ, phê duyệt, và thanh toán...",
  "needsClarification": false,
  "confidence": "high",
  "missingInformation": [],
  "conflicts": [],
  "suggestedFollowUps": [
    "Bạn muốn xem điều kiện phê duyệt theo hạn mức không?"
  ],
  "citations": [
    {
      "sourceType": "Document",
      "title": "Quy trình thanh toán nhà cung cấp",
      "folderPath": "/Finance/Procedures",
      "sectionTitle": "Các bước xử lý",
      "excerpt": "Sau khi tiếp nhận hóa đơn, kế toán kiểm tra chứng từ..."
    }
  ]
}
```

## 10. Workflow Recommendation

Endpoint hiện có cho CRM deal stage:

```text
POST /api/workflow-copilot/deal-stage-changed
```

Auth:

- JWT user.
- User phải có quyền với external object theo ACL snapshot/revalidation.

Payload:

```json
{
  "applicationId": "11111111-1111-1111-1111-111111111111",
  "externalObjectId": "D-10001",
  "fromStage": "Qualification",
  "toStage": "Proposal",
  "idempotencyKey": "deal-D-10001-stage-Proposal-20260605",
  "occurredAt": "2026-06-05T04:00:00Z",
  "dealContextJson": "{\"customer\":\"ABC\",\"amount\":120000000,\"stage\":\"Proposal\"}",
  "notesJson": "[{\"text\":\"Customer requested payment terms.\"}]",
  "tasksJson": "[]",
  "emailsJson": "[]",
  "callsJson": "[]",
  "recentActivitiesJson": "[{\"type\":\"note\",\"summary\":\"Need legal review.\"}]"
}
```

curl:

```bash
curl -X POST "http://localhost:5000/api/workflow-copilot/deal-stage-changed" \
  -H "X-Tenant-Code: acme" \
  -H "Authorization: Bearer <user_jwt>" \
  -H "Content-Type: application/json" \
  -d '{
    "applicationId": "11111111-1111-1111-1111-111111111111",
    "externalObjectId": "D-10001",
    "fromStage": "Qualification",
    "toStage": "Proposal",
    "idempotencyKey": "deal-D-10001-stage-Proposal-20260605",
    "occurredAt": "2026-06-05T04:00:00Z",
    "dealContextJson": "{\"customer\":\"ABC\",\"amount\":120000000,\"stage\":\"Proposal\"}",
    "notesJson": "[{\"text\":\"Customer requested payment terms.\"}]",
    "tasksJson": "[]",
    "emailsJson": "[]",
    "callsJson": "[]",
    "recentActivitiesJson": "[{\"type\":\"note\",\"summary\":\"Need legal review.\"}]"
  }'
```

Response gồm:

- `recommendedNextSteps`
- `risks`
- `clarificationQuestions`
- `suggestedTasks`
- `warnings`
- `wonLostSignals`
- `sources`

Lưu ý: endpoint này hiện chuyên cho CRM deal stage. Nếu muốn review/gợi ý quy trình generic cho ticket, hồ sơ, hợp đồng hoặc tài liệu quy trình, cần bổ sung endpoint/use case tương ứng.

## 11. Postman Setup

### Environment variables

Tạo Postman environment:

| Variable | Example |
| --- | --- |
| `baseUrl` | `http://localhost:5000/api` |
| `tenantCode` | `acme` |
| `adminJwt` | JWT của admin |
| `userJwt` | JWT của user |
| `integrationKeyId` | `crm-prod-key-2026-06` |
| `integrationKey` | Secret integration |
| `applicationCode` | `crm` |
| `applicationId` | GUID application |
| `folderId` | GUID folder |
| `documentId` | GUID document |
| `documentVersionId` | GUID version |

### Collection structure đề xuất

```text
Internal Knowledge Copilot - Third Party Integration
├── 01 Auth
│   └── Login
├── 02 Integration Admin
│   └── Create Integration Connection
├── 03 Document Upload
│   ├── Upload Document
│   ├── Approve Document
│   └── Get Document
├── 04 External Sync
│   ├── Document Changed
│   ├── Object Sync
│   └── Permission Sync
├── 05 AI
│   ├── Ask All
│   ├── Ask Document
│   └── Ask External Object
└── 06 Workflow
    └── Deal Stage Changed
```

### Postman headers

Cho user/admin API:

```text
X-Tenant-Code: {{tenantCode}}
Authorization: Bearer {{userJwt}}
Content-Type: application/json
```

Cho integration API:

```text
X-Tenant-Code: {{tenantCode}}
X-Integration-Key-Id: {{integrationKeyId}}
X-Integration-Key: {{integrationKey}}
Content-Type: application/json
```

Cho upload file trong Postman:

- Method: `POST`
- URL: `{{baseUrl}}/documents`
- Body: `form-data`
- Fields:
  - `folderId`: text
  - `title`: text
  - `description`: text
  - `file`: file
- Không tự set `Content-Type`; Postman sẽ tự tạo boundary.

## 12. Lỗi Thường Gặp

Response lỗi chuẩn:

```json
{
  "error": "error_code",
  "message": "Human readable message"
}
```

| HTTP | Error thường gặp | Nguyên nhân | Cách xử lý |
| --- | --- | --- | --- |
| 400 | `tenant_code_required` | Header `X-Tenant-Code` rỗng | Gửi tenant code hợp lệ. |
| 400 | `idempotency_key_required` | Thiếu `idempotencyKey` | Mỗi event/sync cần một key ổn định. |
| 400 | `duplicate_acl_snapshot` | ACL request có subject/permission trùng | Gộp hoặc loại bản ghi trùng trước khi gửi. |
| 400 | `external_object_scope_incomplete` | Hỏi AI/workflow thiếu object type hoặc id | Gửi đủ `externalObjectType` và `externalObjectId`. |
| 401 | `integration_unauthorized` | Sai `X-Integration-Key` hoặc key id | Kiểm tra secret, connection status, tenant/application. |
| 401 | `invalid_token` | JWT không hợp lệ hoặc hết hạn | Login lại hoặc refresh token theo cơ chế triển khai. |
| 403 | Forbid | User không có quyền folder/object hoặc role không đủ | Kiểm tra folder permission, role Reviewer/Admin, ACL snapshot. |
| 404 | `tenant_not_found` | Tenant code không tồn tại | Kiểm tra tenant đã active và đúng code. |
| 404 | `application_not_found` | Sai `applicationCode` hoặc app inactive | Kiểm tra application của tenant. |
| 404 | `document_not_found` | Sai document id hoặc user không thấy document | Kiểm tra tenant, quyền folder và document id. |
| 409 | `tenant_mismatch` | `X-Tenant-Code` khác tenant claim trong JWT | Dùng token đúng tenant hoặc bỏ header nếu token đã có tenant claim. |
| 409 | `tenant_not_active` | Tenant bị inactive | Kích hoạt tenant trước khi gọi API. |

## 13. Quy Tắc Idempotency

Mọi inbound integration event nên có `idempotencyKey`.

Khuyến nghị format:

```text
<objectType>:<externalObjectId>:<changeType>:<version-or-timestamp>
```

Ví dụ:

```text
procedure:proc-pay-001:object-sync:v3
procedure:proc-pay-001:acl-sync:v3
deal:D-10001:stage:Proposal:20260605T040000Z
```

Nếu retry do timeout/network, gửi lại cùng key. Copilot sẽ trả event cũ với `isDuplicate = true`.

## 14. Bảo Mật Và Vận Hành

- Chỉ gọi production qua HTTPS.
- Không log `X-Integration-Key`, JWT hoặc raw PII.
- Secret nên đủ dài, random, và rotate theo kỳ.
- Mỗi tenant/application nên có secret riêng.
- Không dùng vector metadata làm lớp bảo mật duy nhất; quyền phải được enforce bằng relational ACL hoặc revalidation.
- Với dữ liệu nhạy cảm, hệ thống gốc nên cung cấp `/copilot/permissions/check`.
- Prompt/log/citation cần được xem là dữ liệu nội bộ và bảo vệ theo tenant.
- Khi user rời công ty hoặc đổi quyền, hệ thống gốc phải sync ACL mới ngay.

## 15. Checklist Go-Live

### Tenant và application

- [ ] Tenant đã tồn tại, `status = Active`.
- [ ] Application đã tồn tại, `status = Active`.
- [ ] `applicationCode` thống nhất giữa hai bên.
- [ ] Admin/reviewer/user test đã được tạo đúng tenant.

### Auth và secret

- [ ] JWT login hoạt động với `X-Tenant-Code`.
- [ ] Integration connection đã tạo với `InternalApiKey`.
- [ ] Secret chỉ lưu ở secret manager hoặc nơi an toàn.
- [ ] Đã test sai secret trả 401.
- [ ] Có kế hoạch rotate secret.

### Tài liệu và indexing

- [ ] Upload tài liệu mẫu thành công.
- [ ] Reviewer/Admin approve được document version.
- [ ] Background worker chạy và version chuyển sang `Indexed`.
- [ ] AI Q&A trả lời có citation đúng tài liệu.
- [ ] Tài liệu không được approve không xuất hiện trong AI answer.

### External object và quyền

- [ ] `objects/sync` trả `isDuplicate = false` ở lần đầu.
- [ ] Gửi lại cùng idempotency key trả `isDuplicate = true`.
- [ ] `permissions/sync` tạo ACL snapshot đúng subject.
- [ ] User không có quyền không thấy citation/source nhạy cảm.
- [ ] User có quyền hỏi được dữ liệu tương ứng.
- [ ] Nếu cần, `/copilot/permissions/check` của hệ thống gốc đã sẵn sàng.

### Workflow và action

- [ ] Workflow recommendation được test với object mẫu.
- [ ] Citation trong recommendation trỏ về đúng nguồn.
- [ ] Feedback recommendation hoạt động.
- [ ] Nếu bật action execution, action phải có validate/approval/audit.
- [ ] Hệ thống gốc là nơi thực thi action cuối cùng.

### Quan sát và hỗ trợ

- [ ] Audit log ghi nhận upload, approve, integration event, AI interaction.
- [ ] Có dashboard/log để xem processing job fail.
- [ ] Có quy trình retry inbound event.
- [ ] Có runbook khi AI không tìm thấy tài liệu.
- [ ] Có người chịu trách nhiệm xử lý feedback sai.

## 16. Runbook Kiểm Thử Nhanh

1. Gọi login lấy JWT.
2. Upload file `.md` quy trình.
3. Approve document version.
4. Chờ worker index.
5. Hỏi AI bằng user có quyền.
6. Hỏi cùng câu bằng user không có quyền, xác nhận không thấy source.
7. Gửi `objects/sync` cho object external.
8. Gửi `permissions/sync` cho user test.
9. Gửi lại cùng idempotency key, xác nhận `isDuplicate = true`.
10. Nếu có workflow CRM, gọi `deal-stage-changed` và kiểm tra recommendation/citation.

## 17. Việc Nên Làm Tiếp Theo

Để hoàn thiện partner integration đúng nghĩa, nên bổ sung:

- Worker xử lý `DocumentChanged` để gọi `/copilot/documents/{externalId}/content`, chunk, embed và index external document.
- Endpoint generic cho AI review quy trình, ví dụ `POST /api/process-review/review`.
- Postman collection JSON chính thức trong repo.
- OpenAPI/Swagger spec cho toàn bộ integration API.
- Mapping identity chuẩn giữa Copilot user/team và subject từ hệ thống gốc.
