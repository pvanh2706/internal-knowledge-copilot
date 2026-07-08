# Phase 1 - Pre-sales Hardening

Ngày lập: 2026-07-08

## Mục tiêu

Vá 11 điểm yếu bảo mật/chất lượng đã xác định qua review code, theo mục 7.1 của [KẾ_HOẠCH_THƯƠNG_MẠI_HÓA.md](../../KẾ_HOẠCH_THƯƠNG_MẠI_HÓA.md). Đây là điều kiện bắt buộc trước khi demo cho khách hàng có hiểu biết kỹ thuật.

## File/module liên quan

- Backend: `src/backend/InternalKnowledgeCopilot.Api/Infrastructure/Database/AppDbContext.cs`, `Infrastructure/Tenancy/`, `Infrastructure/Options/JwtOptions.cs`, `Modules/AiSettings/`, `Program.cs`, `appsettings.json`, `Infrastructure/Database/DevelopmentSeeder.cs`
- Frontend: `src/frontend/src/api/http.ts`, `src/frontend/src/api/documents.ts`, `src/frontend/src/pages/auth/LoginPage.vue`, `src/frontend/src/stores/auth.ts`, `src/frontend/package.json`, `src/frontend/tsconfig.json`

## Ràng buộc

- Không đổi API công khai (route, request/response schema) trừ chỗ ghi rõ (mask API key trong response AiSettings).
- Không thêm dependency backend mới ngoài package thuộc ASP.NET Core shared framework; frontend chỉ thêm devDependencies phục vụ lint.
- UI text và comment tiếng Việt theo [QUY_TẮC_CODE.md](../../../QUY_TẮC_CODE.md).
- 97 test backend hiện có phải tiếp tục pass.

## Task

1. **EF global query filter theo tenant** — `AppDbContext` nhận `ITenantContext` tùy chọn; mọi entity có `TenantId` (35 entity) được áp `HasQueryFilter` tự động; scope không có tenant (worker, seeder, test cũ) không bị lọc. Acceptance: ≥ 3 test "tenant A không thấy dữ liệu tenant B" cho documents/wiki/ai_interactions; test "scope không tenant thấy tất cả"; 97 test cũ pass.
2. **Fail-fast JWT signing key** — bỏ default key khỏi `JwtOptions` và `appsettings.json` (chuyển vào `appsettings.Development.json`); khởi động fail với message rõ khi key rỗng, hoặc ở Production khi key < 32 ký tự / trùng key dev đã biết. Acceptance: chạy `ASPNETCORE_ENVIRONMENT=Production` không cấu hình key → throw ngay.
3. **Mã hóa API key AI provider** — `IApiKeyProtector` (IDataProtector, prefix `enc:v1:`) mã hóa `ApiKey`/`EmbeddingApiKey` khi lưu, giải mã khi đọc, tự migrate bản ghi plaintext cũ lúc khởi động (idempotent); response admin trả key dạng masked (`****` + 4 ký tự cuối); update bỏ qua giá trị masked echo lại. Acceptance: test round-trip encrypt/decrypt + passthrough giá trị legacy; giá trị trong DB có prefix `enc:v1:`.
4. **Rate limiting** — policy `ai` (theo user, mặc định 30 req/phút) cho `/api/ai/*`, policy `integrations` (theo integration key, mặc định 120 req/phút) cho `/api/integrations/*`; ngưỡng đọc từ config section `RateLimiting`; vượt ngưỡng trả 429 kèm ApiError. Acceptance: cấu hình được qua config; 429 đúng schema.
5. **Global exception handler** — exception chưa bắt trả JSON `{error, message, traceId}` thống nhất, không lộ stack trace; log đầy đủ server-side. Acceptance: endpoint ném exception bất kỳ trả 500 đúng schema.
6. **Migrate/seed theo môi trường** — `MigrateAsync` chỉ tự chạy khi Development hoặc cờ `Database:MigrateOnStartup=true`; `DevelopmentSeeder` không bao giờ chạy ở Production (guard `IHostEnvironment`). Acceptance: khởi động Production + DB trống không sinh user demo.
7. **`downloadDocument` qua client chung** — thêm `apiRequestBlob` trong `http.ts` gắn `Authorization` + `X-Tenant-Code`, `documents.ts` dùng nó thay `fetch` trần. Acceptance: request download có đủ 2 header.
8. **Xử lý 401 tập trung** — `apiRequest`/`apiRequestBlob` gặp 401 → xóa phiên, chuyển về trang đăng nhập kèm thông báo hết phiên (trừ chính request đăng nhập). Acceptance: token hết hạn ở bất kỳ trang nào → về login, không "đứng hình".
9. **Bỏ credential demo hardcode** — `LoginPage.vue` không chứa email/password mẫu; form khởi tạo rỗng. Acceptance: grep không còn `admin@example.local`/`ChangeMe123!` trong `src/frontend/src`.
10. **engines Node** — `package.json` thêm `"engines": {"node": ">=18"}` + `.npmrc` `engine-strict=true`. Acceptance: npm trên Node < 18 báo lỗi rõ.
11. **ESLint + strict tsconfig** — thêm ESLint (eslint-plugin-vue + typescript-eslint) với script `lint`, bật `strict: true` trong tsconfig, sửa toàn bộ lỗi phát sinh. Acceptance: `npm run lint` và `npm run build` pass sạch.

## Lệnh xác minh cuối phase

```powershell
dotnet test src/backend/InternalKnowledgeCopilot.sln
cd src/frontend
npm run lint
npm run build
npm test
```

## Kết quả gate

Hoàn thành: 2026-07-08. Toàn bộ 11 task đã thực hiện.

- `dotnet test`: **108/108 pass** (97 test cũ + 6 test tenant query filter + 5 test ApiKeyProtector).
- `npm run lint`: pass, 0 error 0 warning (ESLint flat config mới).
- `npm run build`: pass với `strict: true` (vue-tsc không phát sinh lỗi type nào — codebase vốn đã sạch).
- `npm test`: 3/3 pass.
- Kiểm chứng runtime bổ sung:
  - Khởi động `ASPNETCORE_ENVIRONMENT=Production` không cấu hình JWT key → throw ngay lúc startup với message rõ (task 2).
  - Khởi động Production với key hợp lệ + DB trống → app chạy, KHÔNG migrate, KHÔNG seed user demo (task 6); health trả 500 đúng schema `{error, message, traceId}` của global exception handler vì DB chưa migrate (task 5).
  - Khởi động Development → migrate + seed bình thường, health 200, login user demo hoạt động.
  - `/api/documents` với JWT thật trả 200 qua global query filter (task 1).
  - Bắn 35 request liên tiếp vào `/api/ai/ask`: đúng 30 request đầu qua, 5 request sau trả 429 body `{"error":"rate_limited",...}` (task 4).

Ghi chú triển khai:

- Giá trị API key trong DB mang prefix `enc:v1:`; bản ghi plaintext cũ được mã hóa lại tự động lúc khởi động (idempotent). Key ring Data Protection lưu tại `<thư mục DB>/dataprotection-keys` (đổi qua `DataProtection:KeysPath`) — **phải backup cùng DB**, mất key ring là mất khả năng giải mã API key đã lưu.
- Response AiSettings giờ trả API key dạng masked (`****` + 4 ký tự cuối); update bỏ qua giá trị masked echo lại.
- Migrate ở Production chạy qua cờ `Database:MigrateOnStartup=true` (mặc định tắt) — installer/update script sẽ dùng cờ này.
- Ngưỡng rate limiting cấu hình qua section `RateLimiting` (`AiRequestsPerMinute` mặc định 30, `IntegrationRequestsPerMinute` mặc định 120, `Enabled` mặc định true).
- Frontend: task 3 phát hiện và xóa thêm credential hardcode trong `UserManagementPage.vue`; 3 rule ESLint thuần định dạng được tắt có ghi chú (`vue/max-attributes-per-line`, `vue/singleline-html-element-content-newline`, `vue/html-self-closing`).
