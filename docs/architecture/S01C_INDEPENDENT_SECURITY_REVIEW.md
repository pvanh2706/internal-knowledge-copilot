# Review độc lập về bảo mật: S01-C, PR #8 (Bootstrap CLI & Recovery)

**Loại tài liệu:** Báo cáo review độc lập (architecture, security, implementation), gửi PO đánh giá
**Ngày review:** 2026-10-31
**Người review:** Claude Code (review độc lập, không phải tác giả implementation)
**Trạng thái:** Báo cáo review, **không phải** quyết định acceptance. Mọi kết luận cuối thuộc về PO.

> Review được thực hiện read-only trên HEAD `9adff45`. File báo cáo này được commit sau đó theo yêu cầu riêng của người dùng; commit đó chỉ thêm tài liệu, không thay đổi code đã review.

---

## 1. Đối tượng review

| Mục | Giá trị |
|---|---|
| Repository | `pvanh2706/internal-knowledge-copilot` |
| PR | #8, branch `feature/s01-c-bootstrap-recovery` |
| Base SHA | `9b9bf5bad7733ecb7eef8e7a8d9cf60ef2fcb3b3`, khớp với `origin/main` |
| HEAD đã review | `9adff4543cb2472523242ba9d8711bfd624a05b7`, khớp **Expected HEAD** và `refs/pull/8/head` |
| Quy mô | 4 commit, 39 file, +5331/−11 |
| Cách làm | Đọc tĩnh toàn bộ diff, đối chiếu với [Baseline v1.0 §7](S01_TECHNICAL_DESIGN_BASELINE_V1.0.md) và [DEC-0008](../decisions/DEC-0008.md). Chạy lại `dotnet test` cục bộ. Chạy thêm 2 thí nghiệm nhỏ ngoài repo. |
| Giới hạn | Review chỉ đọc: không sửa code, không merge, không gọi AWS. Working tree vẫn sạch sau review. |

## 2. Tóm tắt

- **Phần SQLite/CLI được làm cẩn thận.**
  - Transaction thực sự là `BEGIN IMMEDIATE` (đã kiểm chứng bằng thí nghiệm).
  - Admin, state `Completed` và audit được ghi trong cùng một transaction. Điều kiện được kiểm tra lại bên trong transaction.
  - Ghi vào authority đều là ghi có điều kiện. Mọi trạng thái lệch đều bị từ chối (fail-closed).
  - Recovery có tính idempotent (chạy lại không áp dụng lần hai) và thu hồi token đúng.
- **Phát hiện chính (C-IR-01, P1): "provisioning authority độc lập" không thực sự độc lập với operator.**
  - Sơ đồ chuyển trạng thái chỉ được kiểm tra trong code CLI.
  - Role operator theo thiết kế IAM được quyền `dynamodb:UpdateItem` trên item authority. Với credential đó, operator có thể gọi AWS API trực tiếp và tạo bất kỳ trạng thái nào: `Authorized`, `RecoveryPending`, `Activated`, hoặc gỡ `Revoked`.
  - IAM không thể ràng buộc giá trị hay sơ đồ chuyển trạng thái.
  - `UpdateItem` không có điều kiện sẽ **tạo mới** item, nên câu "không cấp PutItem" trong tài liệu không phải là một biện pháp kiểm soát.
  - Hệ quả: việc ủy quyền recovery không được cưỡng chế một cách độc lập.
- **C-G09 vẫn OPEN và chặn production.** Báo cáo này không đóng gate đó.
- **Test:** 92/92 test S01-C pass. Toàn bộ suite: 356/357, 1 test benchmark của S01-B bị flaky dưới tải. Con số "357/0" do implementer báo cáo **không tái tạo được nguyên vẹn** trong lần chạy này.
- **Khuyến nghị: REMEDIATION REQUIRED** (xem §9).

**Thang mức độ dùng trong báo cáo:**
- **P0:** khai thác được trên code đã ship, hoặc phá ngay một invariant cốt lõi. Chặn merge.
- **P1:** vô hiệu hóa một thuộc tính bảo mật bắt buộc của thiết kế production. Phải có quyết định trước C-EX2 và production.
- **P2:** khiếm khuyết có giới hạn, hoặc lỗi nhưng vẫn fail-closed.
- **P3:** hygiene, chất lượng bằng chứng, tài liệu.

Các phát hiện mới dùng ID **C-IR-xx** (Independent Review), tách biệt với C-R, C-G, S01-R và B-R.

## 3. Danh sách phát hiện

| ID | Mức | Kết luận | Tóm tắt |
|---|---|---|---|
| **C-IR-01** | **P1** | CONFIRMED (phân tích tĩnh + ngữ nghĩa DynamoDB) | Credential operator có thể tạo bất kỳ trạng thái authority nào. Sự độc lập authority/operator không được cưỡng chế. |
| C-IR-02 | P2 | CONFIRMED (tĩnh + chuỗi trong SDK) | `OperatorProfile` kiểu assume-role có MFA không hoạt động: CLI không set `MfaTokenCodeCallback`. |
| C-IR-03 | P2 | CONFIRMED | Kiểm tra quyền nâng cao (elevation) bị bỏ qua chỉ bằng cách đặt biến môi trường `ASPNETCORE_ENVIRONMENT=Testing`. |
| C-IR-04 | P2 | CONFIRMED | Audit bootstrap/recovery không ghi danh tính operator (actor = null). |
| C-IR-05 | P2 | CONFIRMED | Test double DynamoDB và tên test nói nhiều hơn những gì test thực sự chứng minh về các thuộc tính ở Priority 1. |
| C-IR-06 | P3 | CONFIRMED (chạy lại) | `PasswordHasher_Benchmark` (S01-B) flaky dưới tải song song. |
| C-IR-07 | P3 | CONFIRMED | Bước "xác nhận email đích" của recovery hiển thị sẵn đáp án ngay trước khi hỏi. |
| C-IR-08 | P3 | CONFIRMED | TestHarness mặc định coi là `Testing` khi `DOTNET_ENVIRONMENT` không được đặt. |
| C-IR-09 | P3 | CONFIRMED | Preflight chỉ kiểm tra **tên** trigger, không kiểm tra nội dung. |
| C-IR-10 | P3 | CONFIRMED | Test "process crash" thực ra mô phỏng bằng exception trong cùng process. |
| C-IR-11 | P3 | Governance | Định nghĩa C-G01–C-G09, C-R01–C-R05 và C-EX1 không có trong repo. `PROJECT_STATUS.md` đã lỗi thời. |

### C-IR-01 (P1): Authority độc lập không cưỡng chế được trước credential của operator

**Bằng chứng trong code**

- Sơ đồ chuyển trạng thái chỉ được kiểm tra phía client, trong process CLI: [ProvisioningAuthorityMutators.cs:29-50](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Authority/ProvisioningAuthorityMutators.cs#L29-L50) (`EnsureOperatorTransition`).
- `ConditionExpression` do chính người gọi tự đặt ([ProvisioningAuthorityMutators.cs:145-161](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Authority/ProvisioningAuthorityMutators.cs#L145-L161)). Người gọi API trực tiếp chỉ cần bỏ điều kiện đi.
- Mỗi lần ghi, CLI `SET` **toàn bộ** thuộc tính, kể cả các trường thuộc authority: `authorization_id`, `authorized_at`, `authority_id`, `deployment_id`, `record_type`, `schema_version` ([ProvisioningAuthorityMutators.cs:163-166](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Authority/ProvisioningAuthorityMutators.cs#L163-L166), [DynamoDbProvisioningAuthority.cs:54-90](../../src/backend/InternalKnowledgeCopilot.Api/Modules/Platform/Provisioning/DynamoDbProvisioningAuthority.cs#L54-L90)). Vì vậy, nếu giới hạn IAM theo thuộc tính (`dynamodb:Attributes`), CLI hiện tại sẽ không chạy được.
- Runtime chấp nhận mọi bản ghi `Activated` hợp lệ về cấu trúc, không kiểm tra nguồn gốc (ai đã ghi): [ProvisioningEvidence.cs:18-70](../../src/backend/InternalKnowledgeCopilot.Api/Modules/Platform/Provisioning/ProvisioningEvidence.cs#L18-L70). `Validate()` chỉ kiểm tra cấu trúc, và chấp nhận `Revoked` với bất kỳ tổ hợp trường nào ([ProvisioningAuthorityRecord.cs:110](../../src/backend/InternalKnowledgeCopilot.Api/Modules/Platform/Provisioning/ProvisioningAuthorityRecord.cs#L110)).
- Recovery và bootstrap tin hoàn toàn vào bản ghi:
  - Recovery tin `RecoveryPending` và `RecoveryTargetAdminId` ([PlatformRecoveryService.cs:30-69](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Recovery/PlatformRecoveryService.cs#L30-L69)).
  - Bootstrap tin `Authorized` ([PlatformBootstrapService.cs:43-72](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Bootstrap/PlatformBootstrapService.cs#L43-L72)).
- Giả định IAM trong tài liệu ([S01C doc §7, dòng 124](S01C_BOOTSTRAP_RECOVERY_IMPLEMENTATION.md#L124)) cấp cho operator `GetItem` + `UpdateItem` trên item và coi việc "không có PutItem" là đủ. Điều này sai: `UpdateItem` không kèm điều kiện sẽ upsert, tức là tạo item nếu chưa có.

**Kịch bản lạm dụng** (cần credential của role operator; với bước cuối cần thêm quyền admin trên host):

1. Gọi `UpdateItem` trực tiếp lên `pk=deployment#<id>`, không kèm điều kiện, đặt `status=RecoveryPending`, `recovery_purpose=AdminRecovery`, `recovery_target_admin_id=<bootstrap admin>`, cùng một `recovery_id` mới.
2. Chạy `recover-admin` để đặt mật khẩu mới, rồi `Activated`. Audit ghi `platform.recovery.admin_recovered` như thể đã được ủy quyền độc lập.
3. Các biến thể khác:
   - Tạo `Authorized` từ con số không, hoặc sau khi restore DB, để mở lại bootstrap. Việc này phá BOOT-03 / C-G06 khi authority bị thao túng.
   - Đặt `Activated` với `state_version` và `bootstrap_*` khớp một snapshot đã rollback, để vô hiệu cơ chế phát hiện rollback dựa trên `state_version`.
   - Gỡ `Revoked`.

**Trả lời các câu hỏi ở Priority 1:**

| Câu hỏi | Kết luận |
|---|---|
| Kiểm tra chuyển trạng thái trong CLI có đủ làm ranh giới bảo mật không? | **Không.** Nó chỉ bảo vệ khỏi lỗi của operator trung thực, không bảo vệ khỏi người cầm credential. |
| Operator và authority có tách biệt không? | Chỉ theo quy ước. Ai có quyền `UpdateItem` thì có toàn quyền với bản ghi. |
| IAM least privilege / giới hạn theo thuộc tính? | Role runtime (`GetItem` + `LeadingKeys`) là tốt. Với role operator, IAM không ràng buộc được giá trị hay chuyển trạng thái. `dynamodb:Attributes` không phân biệt được SET với REMOVE, và operator buộc phải ghi `status` cùng các trường `recovery_*`. |
| Có chống thao túng trực tiếp qua AWS API không? | Không có biện pháp ngăn chặn. CloudTrail data events trong kế hoạch chỉ giúp phát hiện sau sự việc. |
| Ủy quyền recovery có cưỡng chế độc lập được không? | **Không.** Không có chữ ký hay bằng chứng nguồn gốc nào mà CLI hoặc runtime kiểm tra được. |
| Giả định tin cậy | Operator vốn đã có quyền ghi SQLite (C-R01), nên phần năng lực tăng thêm từ C-IR-01 là: (a) recovery mang danh "được ủy quyền", (b) mở lại bootstrap sau restore, (c) vô hiệu phát hiện rollback, (d) gỡ revoke. Baseline §7.2/§7.6/§7.7 và S01-R04 (P0) dựa đúng vào sự độc lập này. Nếu mô hình đe dọa của PO bao gồm operator độc hại hoặc bị chiếm quyền, nên coi mức này là **P0 đối với production**. |

**Các phương án khắc phục (đều cần PO phê duyệt):**

- **(B) Khuyến nghị: dịch vụ chuyển trạng thái trung gian.**
  - Operator không còn bất kỳ quyền ghi DynamoDB nào, chỉ có `lambda:InvokeFunction` (hoặc API Gateway xác thực bằng IAM) tới một function thuộc tài khoản authority.
  - Function đó cưỡng chế phía server: sơ đồ chuyển trạng thái; `state_version` = 1 khi bootstrap; = trước +1 khi recovery; = 2 khi reconcile.
  - Function ghi lại danh tính người gọi.
  - Đây là thành phần hạ tầng mới, nằm trong phạm vi C-EX2.
- **(A) Grant có chữ ký.**
  - Authority ký `Authorized` / `RecoveryPending` / `Revoked` bằng khóa bất đối xứng KMS (chỉ role authority có `kms:Sign`).
  - CLI và runtime xác minh bằng public key được ghim sẵn. Grant gắn với deployment, purpose, target, version và hạn dùng.
  - Hạn chế: chặn được việc giả grant, nhưng operator vẫn sửa được giá trị lúc `Activated`.
- **(C) Tách item.** Item grant do authority sở hữu (operator chỉ có `GetItem`) và item tiến trình do operator ghi, dùng `ConditionCheck` qua `TransactWriteItems`. Hạn chế tương tự (A).
- **Bắt buộc dù chọn phương án nào:**
  - Sửa tài liệu §7: nêu rõ `UpdateItem` upsert và IAM không cưỡng chế được chuyển trạng thái.
  - Cảnh báo qua DynamoDB Streams hoặc CloudTrail cho mọi thao tác ghi không khớp sơ đồ chuyển trạng thái.
  - Role operator chỉ được cấp kịp thời (just-in-time) và có MFA.
- **Phương án tối thiểu, không đổi code:** PO chính thức chấp nhận rủi ro "operator được tin tuyệt đối" và **sửa lời văn** của Baseline §7.6. Không khuyến nghị phương án này.

**Gate cần thông qua:** phải có quyết định thiết kế (có thể ghi thành một DEC mới) **trước C-EX2**, vì đầu vào IAM của C-EX2 hiện đang sai. Mọi thay đổi code cần một ủy quyền remediation mới.

### C-IR-02 (P2): Profile assume-role có MFA không hoạt động

- [PlatformCliContext.cs:167](../../src/backend/InternalKnowledgeCopilot.PlatformCli/PlatformCliContext.cs#L167) dùng `CredentialProfileStoreChain().TryGetAWSCredentials` nhưng không set `MfaTokenCodeCallback`.
- Trong `AWSSDK.Core 4.0.100` có chuỗi lỗi: *"The MfaSerialNumber has been set but the MfaTokenCodeCallback hasn't…"*.
- Profile SSO cũng không dùng được, vì project không tham chiếu `AWSSDK.SSO` / `AWSSDK.SSOOIDC`.
- Comment trong csproj ([dòng 13](../../src/backend/InternalKnowledgeCopilot.PlatformCli/InternalKnowledgeCopilot.PlatformCli.csproj#L13)) nói hỗ trợ `mfa_serial`, điều này không đúng.
- **Hậu quả:**
  - Lỗi vẫn fail-closed: CLI trả exit 3.
  - Nhưng operator sẽ bị đẩy sang dùng access key tĩnh dài hạn cho đúng role có `UpdateItem`, làm tăng rủi ro của C-IR-01.
  - Điều này mâu thuẫn với tài liệu §7 và yêu cầu MFA ở baseline §5.
- **Ghi chú:** chưa kiểm chứng trên AWS thật; cần đưa vào C-EX2.

### C-IR-03 (P2): Kiểm tra elevation phụ thuộc biến môi trường do operator kiểm soát

- [PlatformCliApplication.cs:271](../../src/backend/InternalKnowledgeCopilot.PlatformCli/PlatformCliApplication.cs#L271) bỏ qua `Environment.IsPrivilegedProcess` khi môi trường là Development/Testing.
- Tên môi trường lấy từ `ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT` ([PlatformCliContext.cs:56-59](../../src/backend/InternalKnowledgeCopilot.PlatformCli/PlatformCliContext.cs#L56-L59)).
- Provider `DynamoDb` được phép chạy ở **mọi** môi trường.
- Kết quả: đặt `ASPNETCORE_ENVIRONMENT=Testing` là bỏ qua được elevation. Kiểm soát thực sự còn lại chỉ là ACL của file DB và credential AWS.
- `IsPrivilegedProcess` chỉ có nghĩa là "một admin cục bộ bất kỳ", không xác định operator cụ thể nào (trùng một phần với C-R04).

### C-IR-04 (P2): Audit thiếu danh tính operator

- Audit bootstrap và recovery ghi `actor_platform_id = null`, metadata chỉ gồm id và mã ([PlatformBootstrapService.cs:201-213](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Bootstrap/PlatformBootstrapService.cs#L201-L213), [PlatformRecoveryService.cs:128-152](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Recovery/PlatformRecoveryService.cs#L128-L152)).
- Không có tài khoản OS, host hay ARN của người gọi AWS. Baseline §9 / AUD-01 yêu cầu "correct actor".
- Với LocalSeal và với các thao tác ghi SQLite, không còn nguồn log nào khác để truy vết.

### C-IR-05 (P2): Bằng chứng test cho Priority 1 bị phóng đại

- [FakeDynamoDb.cs:99-101](../../src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/FakeDynamoDb.cs#L99-L101) từ chối **mọi** `UpdateItem` khi item chưa tồn tại, bất kể điều kiện. DynamoDB thật thì upsert.
  - Vì thế `Mutator_MissingItem_IsNeverCreated` vẫn pass kể cả khi bỏ `attribute_exists`.
  - Test double này mã hóa đúng giả định đang che C-IR-01.
- `OperatorTransitions_CannotCreateAuthorizationRecoveryOrRevocation` ([S01CDynamoDbAuthorityTests.cs:174-215](../../src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/S01CDynamoDbAuthorityTests.cs#L174-L215)) chỉ chứng minh *code CLI* không thực hiện các chuyển đó, không chứng minh *operator* không làm được.
- "IAM read-only" ở C-G02 thực chất là cờ `allowWrites` của test double.

### C-IR-06 đến C-IR-11 (P3)

- **C-IR-06:** `PasswordHasher_Benchmark` (S01-B) fail trong lần chạy full suite: tỷ lệ 0.235, nằm ngoài [0.5, 2.0]. Chạy riêng thì pass 3/3. Nguyên nhân là tải CPU từ các test S01-C đa process / Argon2 chạy song song. Đây là vấn đề độ tin cậy của CI, không phải lỗ hổng.
- **C-IR-07:** [PlatformCliApplication.cs:210](../../src/backend/InternalKnowledgeCopilot.PlatformCli/PlatformCliApplication.cs#L210) in email đích ngay trước khi [:232](../../src/backend/InternalKnowledgeCopilot.PlatformCli/PlatformCliApplication.cs#L232) yêu cầu gõ lại email đó. Bước này chỉ chứng minh operator có chú ý, không phải xác minh danh tính. "Operator verification" theo §7.6 phải là quy trình nghiệp vụ (C-R05).
- **C-IR-08:** [TestHarness/Program.cs:10](../../src/backend/InternalKnowledgeCopilot.PlatformCli.TestHarness/Program.cs#L10) mặc định là `Testing` và bỏ qua `ASPNETCORE_ENVIRONMENT`. Mode `cli` nhận mật khẩu qua pipe và bỏ qua elevation. Rủi ro đã được giảm nhờ `IsPublishable=false` và quy trình IIS chỉ publish project Api, nhưng chạy `dotnet build` cả solution trên host vẫn sinh ra binary của harness.
- **C-IR-09:** [PlatformDatabaseInspector.cs:154-157](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Preflight/PlatformDatabaseInspector.cs#L154-L157) chỉ so tên trigger. Một trigger bị thay bằng bản no-op cùng tên, hoặc một trigger lạ thêm vào, vẫn qua preflight.
- **C-IR-10:** `FaultPoint.Crash` ([S01CTestEnvironment.cs:319-320](../../src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/S01CTestEnvironment.cs#L319-L320)) chỉ là ném exception. Không có test kill process thật giữa lúc commit và lúc activate.
- **C-IR-11:**
  - Kế hoạch S01-C (C-EX1, định nghĩa C-G01–C-G09, C-R01–C-R05) không có trong repo. Chính tài liệu §10.1 thừa nhận các ID rủi ro do implementer tự đặt. Vì vậy **không thể xác minh** C-R01–C-R05 có nhất quán với tài liệu PO đã duyệt hay không.
  - `PROJECT_STATUS.md` vẫn ghi "Implementation NOT AUTHORIZED" (tài liệu do PO sở hữu).

## 4. Đánh giá C1–C6

Định nghĩa C1–C6 không có trong repo. Cách ánh xạ dưới đây suy ra từ comment trong code và tài liệu triển khai.

| Hạng mục | Đánh giá |
|---|---|
| C1 CLI | Đã triển khai. Xử lý secret tốt: không nhận qua argument, không echo, từ chối stdin bị chuyển hướng, output không chứa secret. Tồn tại: C-IR-03, C-IR-07, C-IR-02. |
| C2 Preflight | Đúng: migration khớp chính xác, có đủ 5 trigger, DB nguyên trạng, authority ở `Authorized`. Tồn tại: C-IR-09. |
| C3 Authority | Reader/mutator/LocalSeal đúng ở mức code. Reader dùng `ConsistentRead` và map lỗi thành reason code an toàn. **Có lỗ hổng thiết kế C-IR-01.** Test double lệch so với thực tế (C-IR-05). |
| C4 Bootstrap | Đúng: thứ tự reserve → SQLite → activate → đọc lại; Reserved không bao giờ hết hạn; đọc lại khi phản hồi bị mất. |
| C5 Recovery | Idempotent theo `recovery_id`; `token_version+1`; `FINALIZE` không áp dụng lại credential. Tính độc lập bị ảnh hưởng bởi C-IR-01; audit thiếu danh tính (C-IR-04). |
| C6 Tests/docs (suy ra) | 92 test có giá trị ở mức code. Một số nói quá (C-IR-05, C-IR-10). Giả định IAM ở §7 sai (C-IR-01). Có test flaky (C-IR-06). |

**Các điểm ở Priority 2 đã xác nhận đạt:**
- Transaction là IMMEDIATE (thí nghiệm: writer thứ hai nhận `SQLITE_BUSY`).
- Có kiểm tra lại bên trong transaction ([PlatformBootstrapService.cs:172](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Bootstrap/PlatformBootstrapService.cs#L172)).
- Bảng state có singleton PK.
- Audit bắt buộc ghi lỗi thì toàn bộ transaction rollback.
- Chặn TOCTOU bằng reserve có điều kiện cộng kiểm tra lại trong transaction.
- Xử lý lệch authority/SQLite qua `Diagnose` đầy đủ và fail-closed.
- Restore SQLite trước bootstrap bị chặn; snapshot thuộc epoch cũ hơn bị phát hiện.

**Không có trong PR:** thay đổi schema/migration, mã S01-D/E/F, script deploy. Các thay đổi S01-A/B tương thích ngược; `Provider=None` giữ nguyên hành vi S01-B.

## 5. Đánh giá C-G01 đến C-G09

| Gate | Implementer tự đánh giá | Đánh giá độc lập |
|---|---|---|
| C-G01 | PASS | **PASS** |
| C-G02 | PASS (code) | **PASS ở mức code.** IAM chưa được xác minh (C-EX2). |
| C-G03 | PASS | **PASS.** Đã kiểm chứng `BEGIN IMMEDIATE`. |
| C-G04 | PASS | **PASS.** Đa process chỉ được test với LocalSeal; DynamoDB chỉ test in-process trên test double. |
| C-G05 | PASS (test double) | **PASS ở mức code/test double.** Đường credential MFA hỏng nhưng vẫn fail-closed (C-IR-02). |
| C-G06 | PASS / một phần | **CÓ ĐIỀU KIỆN.** Đúng nếu authority không bị thao túng. Credential operator có thể mở lại bootstrap (C-IR-01). |
| C-G07 | PASS | **MỘT PHẦN.** Code đúng, nhưng "ủy quyền độc lập" không cưỡng chế được (C-IR-01) và thiếu danh tính trong audit (C-IR-04). |
| C-G08 | PASS (357/0) | **MỘT PHẦN.** 92/92 tái tạo được; full suite 356/357 (C-IR-06); không phải bằng chứng CI; AWS còn chờ C-EX2. |
| **C-G09** | OPEN | **OPEN, chặn production. Không đóng.** |

**Đánh giá C-G09, không mở rộng phạm vi:**
- `state_version` chỉ tăng khi recovery.
- Các thay đổi bảo mật của S01-B (đổi mật khẩu, khóa tài khoản, disable, thu hồi token) **không** làm tăng `state_version`. Vì vậy restore bất kỳ snapshot `Completed` nào trong cùng một epoch sẽ âm thầm hoàn tác các thay đổi đó. Cụ thể: token đã thu hồi sống lại cho tới khi hết TTL, mật khẩu đã bị lộ sống lại.
- Khi S01-D thêm vòng đời quản lý admin, cửa sổ rủi ro này sẽ rộng hơn.
- Các hướng xử lý (đều cần PO quyết định; hướng (b) xung đột với thiết kế runtime chỉ đọc C-G02):
  - (a) Mỗi lần restore bắt buộc qua một epoch recovery do authority điều phối.
  - (b) Bộ đếm epoch đặt bên ngoài, tăng ở mọi thao tác bảo mật đặc quyền.
  - (c) Neo chuỗi hash của audit vào kho append-only bên ngoài (ví dụ S3 Object Lock).
  - (d) Thắt chặt TTL của JWT.

## 6. Kết quả test độc lập

- **Lệnh:** `dotnet test src/backend/InternalKnowledgeCopilot.sln`
- **Môi trường:** .NET SDK 10.0.101, target net8.0, Windows 11 Pro, máy cục bộ (không phải CI).
- **Kết quả:**
  - Tổng 357: **356 Passed, 1 Failed, 0 Skipped** (1 phút 15 giây).
  - **S01-C: 92/92 Passed.** Danh sách test khớp đúng 92 test được khai báo.
  - Test fail: `PlatformSecurityPrimitivesTests.PasswordHasher_Benchmark` (S01-B). Chạy riêng lại 3 lần đều pass, nên đây là flaky (C-IR-06).
- **File TRX:** lưu cục bộ trên máy review, không commit vào repo.
- **Thí nghiệm bổ sung** (thực hiện ngoài repo):
  1. EF Core 8.0.11 `BeginTransactionAsync()` trên SQLite là Serializable/IMMEDIATE.
  2. Xác nhận yêu cầu `MfaTokenCodeCallback` trong AWSSDK.Core 4.0.100.
- **CHƯA chạy / không kiểm chứng được:**
  - AWS thật: IAM, conditional write, ConsistentRead, throttling, assume-role (không được ủy quyền).
  - Kill process thật.
  - Linux / IIS.
  - Restore drill trên bản backup thật.
  - CI độc lập.

## 7. Khuyến nghị khắc phục và yêu cầu phê duyệt

| ID | Khắc phục | Cần PO phê duyệt mới? |
|---|---|---|
| C-IR-01 | Quyết định kiến trúc (B khuyến nghị, hoặc A/C), sửa tài liệu §7, thêm cảnh báo, role operator JIT + MFA | **Có:** quyết định thiết kế / DEC; thành phần mới thuộc phạm vi C-EX2; code cần ủy quyền remediation |
| C-IR-02 | Thêm `MfaTokenCodeCallback` (nhập không echo) hoặc hỗ trợ SSO | Có (vòng remediation; SSO thêm dependency mới) |
| C-IR-03 | Elevation luôn bắt buộc; chỉ hook test trong code mới được miễn | Có (vòng remediation, trong phạm vi S01-C) |
| C-IR-04 | Ghi OS user/host và ARN người gọi AWS vào metadata (mở rộng allowlist) | Có (đổi allowlist; cân nhắc quyền riêng tư) |
| C-IR-05 | Test double mô phỏng upsert; thêm test đột biến bỏ điều kiện; đổi tên test cho đúng | Có (vòng remediation) |
| C-IR-06 | Tách benchmark khỏi chạy song song | Có (chạm test S01-B) |
| C-IR-07 đến C-IR-10 | Bổ sung runbook; harness fail-closed; kiểm tra nội dung trigger; đổi tên hoặc thêm test kill process thật | Có (vòng remediation, P3, có thể làm gộp) |
| C-IR-11 | Công bố kế hoạch S01-C và định nghĩa C-G/C-R; cập nhật `PROJECT_STATUS.md` | Việc của PO |

## 8. Các điểm còn chặn production

1. **C-G09 / C-R01:** rollback trạng thái đặc quyền vẫn OPEN.
2. **C-IR-01:** tính độc lập của authority không cưỡng chế được.
3. **C-R02 / C-EX2:** chưa có bằng chứng AWS (bao gồm C-IR-02).
4. **C-R05:** quy trình của authority độc lập chưa được định nghĩa / triển khai.
5. **C-R04 cùng yêu cầu MFA đặc quyền** (baseline §5).
6. **C-R03:** việc chấp nhận rủi ro availability đang chờ PO.
7. **Governance:** C-IR-11.

## 9. Kết luận cuối

**REMEDIATION REQUIRED**

- Không có P0 trong code hiện tại. Phần SQLite/CLI đạt chất lượng tốt.
- Tuy nhiên, giả định tin cậy cốt lõi của thiết kế production (authority độc lập với operator) đang sai (C-IR-01, P1). Một số gate được tự đánh giá PASS nhưng bằng chứng không đủ (C-G06, C-G07, C-G08).
- Cần PO quyết định về C-IR-01 **trước C-EX2**, và một vòng remediation cho các mục P2.
- **C-G09 vẫn OPEN.** PR #8 **không được merge** theo phạm vi review này.
