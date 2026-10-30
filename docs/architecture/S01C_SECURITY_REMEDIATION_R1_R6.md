# S01-C: Security Remediation R1–R6 (Authority Service phía server)

**Gói:** S01-C-REM-EX1 (Coding Authorization v1.0, PO đã duyệt).
**Branch:** `feature/s01-c-security-remediation`, stacked trên `feature/s01-c-bootstrap-recovery`.
**Base SHA đã kiểm chứng trước khi sửa:** `7101d843e0017d54c4ca73fe5d455a4510c3f1ca`.
**Mức kiểm chứng:**
- Test tự động chạy trên SQLite thật và tiến trình OS thật.
- DynamoDB, API Gateway và STS chỉ là **test double chạy trong tiến trình**.
- Không tạo hay gọi bất kỳ tài nguyên AWS nào.

> IMPLEMENTED ≠ VERIFIED IN AWS ≠ ACCEPTED. Tài liệu này **không đóng** finding hay gate nào. **C-G09 vẫn OPEN và chặn production.** Mọi trạng thái dưới đây đều chờ independent security re-review và các bước C-EX2/C-EX3.

---

## 0. Tài liệu nguồn và khoảng trống governance

| Tài liệu | Trạng thái trong repo |
|---|---|
| Independent review C-IR-01–C-IR-11 | Có: [S01C_INDEPENDENT_SECURITY_REVIEW.md](S01C_INDEPENDENT_SECURITY_REVIEW.md) |
| Contract Freeze CF-01–CF-05 | Chỉ có trong prompt ủy quyền (được trích lại ở §4) |
| S01-C-ADR-01, S01-C Detailed Security Design v1.0, Remediation Plan R1–R6 v1.0 | **Không có trong repo** |
| REM-G01–REM-G10 | **Không có định nghĩa** trong repo hay trong prompt |

- Implementation chỉ bám vào các ràng buộc ghi trong prompt (CF-01–CF-05, R1–R6) và các khuyến nghị đã nêu của review độc lập.
- Không có quyết định kiến trúc nào mới được tự đặt ngoài phạm vi đó. Các lựa chọn chi tiết (route, mô hình item, mã lý do) đều liệt kê ở §2 để PO xác nhận.
- REM-G01–G10 **không thể đánh giá** (xem §5).

## 1. Tóm tắt

- **C-IR-01 (P1) được khắc phục ở mức code.**
  - Operator **không còn bất kỳ đường ghi DynamoDB nào**: `DynamoDbProvisioningAuthorityMutator` đã bị xóa. CLI chỉ gọi các route operation của Authority Service qua API Gateway (SigV4, IAM auth).
  - Sơ đồ chuyển trạng thái, approval, tách nhiệm vụ, ledger và audit được cưỡng chế ở **server**.
  - Template IAM cấm tường minh mọi quyền ghi DynamoDB của operator và approver.
- **Các P2 (C-IR-02..05) đã được xử lý trong code:**
  - Credential assume-role bắt buộc MFA, có callback nhập mã.
  - Kiểm tra nâng quyền không còn phụ thuộc tên môi trường.
  - Audit SQLite và audit của service đều ghi danh tính người thực hiện.
  - Test double DynamoDB đã có ngữ nghĩa upsert, và các test nói quá đã được đổi tên.
- **Các P3 (C-IR-06..10) đã được xử lý.** C-IR-11 vẫn là việc governance của PO.
- **Test:** build `--no-incremental` cho 0 warning, 0 error. `dotnet test` toàn solution đạt **414/414 ở cả 3 lần chạy liên tiếp**. S01-C có 149 test (trước đó 92).
- **Chưa kiểm chứng trên AWS:** IAM thật, API Gateway, Lambda, STS/MFA, DynamoDB transaction thật. Adapter Lambda và công cụ cho approver chưa được viết (xem §9).

## 2. Kiến trúc sau remediation (R1/R2)

```
 Security Approver (role riêng/người, MFA)          Deployment Operator (role riêng/người, MFA) — trên host, phiên nâng quyền
        │ SigV4: POST /authority/approvals                │ CLI: bootstrap / recover-admin
        ▼                                                 ▼ SigV4: POST /authority/operations/{reserve|activate|claim-recovery|complete-recovery}
 ┌──────────────────────── API Gateway (authorization AWS_IAM) ─────────────────────────┐
 │ requestContext.identity (accountId, userArn, caller) do AWS điền sau xác minh SigV4  │
 └───────────────────────────────────────────┬───────────────────────────────────────────┘
                                             ▼ (Lambda chỉ invoke được từ API này)
                  Authority Service — AuthorityServiceEndpoint
                  1. từ chối lời gọi không có ngữ cảnh gateway/IAM (gọi Lambda trực tiếp)
                  2. TrustedPrincipalResolver: assumed-role ARN + role unique id → registry (Security Ops) → person_id
                  3. route approval ⇒ Approver; route operation ⇒ Operator
                  4. body JSON chặt (trường lạ, kể cả "caller_person_id", bị từ chối)
                  5. ledger replay theo request_id (fingerprint + người gọi)
                  6. AuthorityApprovalHandler | AuthorityTransitionHandler
                  7. TransactWriteItems: Put có điều kiện (attribute_not_exists / version = expected) + ledger
                  8. audit JSON có cấu trúc
                                             ▼
                  DynamoDB (bảng authority): deployment#, approval#, issuance#, ledger#, registry#principals
                                             ▲ GetItem(ConsistentRead) deployment#<id> — runtime API và CLI (đọc)
```

### 2.1 Các item trong bảng authority

| Key | Nội dung | Ai ghi |
|---|---|---|
| `deployment#<id>` | Bản ghi authority, **định dạng không đổi** so với S01-C (runtime đọc như cũ) | Authority Service |
| `approval#<operation_id>` | Approval gắn với deployment, action, target, `authority_version`, operator được chỉ định, người phê duyệt, `expires_at`, trạng thái `Issued → Consumed → Completed` (hoặc `Superseded`) | Authority Service |
| `issuance#<deployment_id>` | Lịch sử cấp bootstrap, không bao giờ xóa (protected tombstone) | Authority Service |
| `ledger#<request_id>` | Request đã áp dụng: route, fingerprint, `person_id`, ARN role, operation, version sau | Authority Service (cùng transaction với thay đổi) |
| `registry#principals` | Ánh xạ role unique id + role ARN → `person_id`, chức năng Operator/Approver, các deployment được phép, trạng thái active | **Security Operations** (service chỉ đọc; IAM cấm service ghi) |

### 2.2 Sơ đồ chuyển trạng thái cưỡng chế ở server

| Route | Ai gọi | Chuyển trạng thái | Ràng buộc chính |
|---|---|---|---|
| `approvals` / `Bootstrap` | Approver | không có bản ghi → `Authorized` v1 | Chưa có bản ghi **và** chưa có `issuance#`; operator ≠ approver |
| `approvals` / `ReauthorizeBootstrap` | Approver | `Reserved` (bị gián đoạn) → `Authorized`; hoặc thay `Authorized` có approval đã hết hạn chưa dùng | Không bao giờ từ `Activated`, `RecoveryPending`, `Revoked`; có version |
| `approvals` / `AdminRecovery` | Approver | `Activated` → `RecoveryPending(AdminRecovery, target)` | Bắt buộc có target; version |
| `approvals` / `BootstrapReconciliation` | Approver | `Reserved` → `RecoveryPending(BootstrapReconciliation)` | version |
| `approvals` / `Revoke` | Approver | mọi trạng thái (trừ `Revoked`) → `Revoked` | **Không có route nào gỡ `Revoked`** |
| `reserve` | Operator | `Authorized` → `Reserved` (`reservation_id = operation_id`) | Đúng operator được chỉ định, approval còn `Issued`, chưa hết hạn; single-use |
| `activate` | Operator | `Reserved` → `Activated` | Chính người đã nhận approval; server tự đặt `bootstrap_admin_id = reserved_admin_id`, `state_version = 1`; thời điểm bằng chứng phải hợp lý |
| `claim-recovery` | Operator | không đổi bản ghi; approval `Issued → Consumed` | Như reserve. Cùng người gọi lại thì idempotent |
| `complete-recovery` | Operator | `RecoveryPending` → `Activated` | Chính người đã nhận; `state_version` do **server** tính (AdminRecovery: trước + 1; BootstrapReconciliation: 2) |

Các ràng buộc chung: `expected_authority_version` phải khớp; người phê duyệt không được thực hiện, kể cả qua role hay session khác; approval không dùng được cho deployment khác; mọi bản ghi kết quả phải qua `Validate()`.

### 2.3 CLI (R2)

- Giữ nguyên các lệnh `status`, `recovery-check`, `bootstrap`, `recover-admin`, các exit code, prompt mật khẩu không echo và transaction SQLite `BEGIN IMMEDIATE`.
- **Bootstrap:**
  1. Preflight.
  2. `reserve` qua service, thử lại với **cùng `request_id`** khi kết quả không rõ.
  3. Consistent read xác nhận reservation của chính tiến trình này.
  4. Chỉ ghi SQLite khi đã có `person_id` được service xác minh.
  5. `activate`.
  6. Verify bằng đúng quy tắc runtime.
- **Recovery:** `claim-recovery` → transaction SQLite (`token_version+1`, `state_version+1`, audit) → `complete-recovery` → verify. Lần chạy `FINALIZE` chỉ hoàn tất, không bao giờ áp dụng lại credential.
- **Đọc authority:** luôn đi qua đường read-only của runtime. Với DynamoDB, đó là default credential chain của host, tức role runtime chỉ có `GetItem`.
- **LocalSeal (chỉ Development/Testing):** vẫn tự chuyển trạng thái dưới khóa file. **Đây không phải ranh giới bảo mật.** Danh tính được ghi là `local_seal_unverified`.

## 3. Kết quả R1–R6

| Gói | Kết quả | File chính |
|---|---|---|
| **R1** Authority Service | Approval Handler, Transition Handler, resolver danh tính, registry, mô hình approval, state machine, ledger, store có điều kiện, chống replay/đồng thời, audit có cấu trúc, adapter test | `InternalKnowledgeCopilot.AuthorityService/*` (project mới, không deploy) |
| **R2** CLI | Đường ghi DynamoDB trực tiếp bị xóa. `AuthorityServiceHttpClient` (SigV4), `AuthorityCalls.WithRetryAsync` (cùng `request_id`), xác nhận bằng consistent read, danh tính trong audit | `PlatformCli/Authority/*`, `Bootstrap/*`, `Recovery/*`, `PlatformCliContext.cs` |
| **R3** IAM / danh tính / audit | 6 template: runtime chỉ đọc; operator chỉ route operation; approver chỉ route approval; service ghi có phạm vi, cấm xóa và cấm sửa registry; Lambda chỉ invoke qua API; trust policy theo người, có MFA. Kèm test âm bằng bộ mô phỏng policy cô lập | `AuthorityService/Policies/*.json`, `S01CIamPolicyTests` |
| **R4** P2 | C-IR-02..05 (xem §4) | `OperatorCredentialProvider.cs`, `PlatformCliApplication.cs`, `PlatformAuditWriter.cs`, `FakeDynamoDb.cs` |
| **R5** Test hardening | C-IR-06..10 (xem §4) | `PlatformPasswordHasherBenchmarkTests`, `PlatformDatabaseInspector.cs`, TestHarness, `S01CProcessTerminationTests` |
| **R6** Governance | Tài liệu này. Banner "superseded" trong tài liệu implementation S01-C | `docs/architecture/*` |

## 4. Ma trận C-IR-01 đến C-IR-11

Trạng thái dùng trong bảng: **REMEDIATED (code)** nghĩa là đã khắc phục ở mức code và test cô lập, chờ re-review độc lập. **NOT VERIFIED (AWS)** nghĩa là phần còn lại cần C-EX2. Không mục nào được đánh dấu CLOSED.

| ID | Mức | Trạng thái | Khắc phục | Bằng chứng (test) |
|---|---|---|---|---|
| C-IR-01 | P1 | REMEDIATED (code); NOT VERIFIED (AWS) | Authority Service phía server. Operator không có quyền ghi DynamoDB (code và template IAM). Server tự tính `state_version` và admin bootstrap. Có tombstone cấp phát. Không có route gỡ `Revoked` | `S01CAuthorityServiceTests.*` (22 test case), `S01CIamPolicyTests.DeploymentOperator_*`, `UnconditionedUpdateItem_UpsertsAnyState_*`, `S01CDynamoDbAuthorityTests.AuthorityChanges_GoOnlyThroughAuthorityServiceConditionalTransactions` |
| C-IR-02 | P2 | REMEDIATED (code); NOT VERIFIED (STS thật) | `OperatorCredentialProvider` chỉ nhận profile assume-role có `mfa_serial`, tự đặt `MfaTokenCodeCallback` (nhập không echo), session 900 giây. Từ chối access key tĩnh, SSO, `credential_process` và `credential_source` | `OperatorCredentials_AssumeRoleProfileWithMfa_UsesInteractiveMfaCallback`, `OperatorCredentials_WithoutMfaOrAssumeRole_AreRejected` |
| C-IR-03 | P2 | REMEDIATED (code) | Thao tác ghi luôn yêu cầu `Environment.IsPrivilegedProcess`, bất kể tên môi trường. Ngoại lệ duy nhất là `PrivilegeCheck`, chỉ đặt được từ code (test, TestHarness chỉ dùng LocalSeal). Ranh giới thật nằm ở server | `Cli_Mutation_RequiresElevatedOperatorInEveryEnvironmentBeforeAnyAuthorityAccess` (Production/Testing/Development) |
| C-IR-04 | P2 | REMEDIATED (code) | Audit SQLite có thêm metadata `operator_person_id`, `approver_person_id`, `identity_assurance` (allowlist mở rộng thêm 3 khóa). SQLite không được ghi nếu thiếu danh tính đã xác minh. Audit service và ledger đều mang `person_id` cùng ARN role | `BootstrapAndRecoveryAudit_RecordOperatorIdentityAndAssurance` (2), `MissingVerifiedOperatorIdentity_*` (2), `AuditEvents_CarryVerifiedIdentity_AndRejectionsCarryObservedPrincipal` |
| C-IR-05 | P2 | REMEDIATED (test) | `FakeDynamoDb` nhiều item. `UpdateItem` không điều kiện sẽ **upsert**. `TransactWriteItems` nguyên tử, có `CancellationReasons`. Test nói quá đã đổi tên. Mọi file test ghi rõ đây không phải bằng chứng IAM | `UnconditionedUpdateItem_*`, `S01CDynamoDbAuthorityTests` (đã viết lại) |
| C-IR-06 | P3 | REMEDIATED (test) | Benchmark tách sang collection `DisableParallelization` và đo xen kẽ theo cặp. Assertion giữ nguyên | `PlatformPasswordHasherBenchmarkTests.PasswordHasher_Benchmark`; 3 lần chạy full-suite đều pass |
| C-IR-07 | P3 | REMEDIATED (code); thủ tục nghiệp vụ C-R05 vẫn OPEN | CLI chỉ in id tài khoản. Operator phải nhập email lấy từ yêu cầu recovery đã duyệt | `RecoveryTargetConfirmation_DoesNotRevealTheExpectedEmail` |
| C-IR-08 | P3 | REMEDIATED (code) | TestHarness không còn môi trường mặc định. Bắt buộc `DOTNET_ENVIRONMENT` là Development hoặc Testing, `ASPNETCORE_ENVIRONMENT` (nếu có) phải trùng, và provider phải là LocalSeal | `TestHarness_RefusesOutsideExplicitTestingWithLocalSeal` (4) |
| C-IR-09 | P3 | REMEDIATED (code) | Preflight so **nội dung SQL đã chuẩn hóa** của 5 trigger S01-A và từ chối mọi trigger lạ trên bảng platform | `Preflight_DetectsTamperedOrForeignPlatformTriggers` (3) |
| C-IR-10 | P3 | REMEDIATED (test) | Có test kill tiến trình thật ngay sau commit SQLite (hook dừng của harness). Test exception cũ được đổi tên rõ là exception injection | `S01CProcessTerminationTests` (2), `Bootstrap_ExceptionInjectedBetweenCommitAndActivation_*` |
| C-IR-11 | P3 | OPEN (governance, việc của PO) | Tài liệu này nêu rõ các tài liệu còn thiếu (§0). `PROJECT_STATUS.md` không bị sửa | — |

## 5. CF-01 đến CF-05 và REM-G01 đến REM-G10

| CF | Kết quả | Ghi chú |
|---|---|---|
| CF-01 Danh tính | **Tuân thủ ở mức code.** Danh tính chỉ lấy từ `requestContext.identity` (assumed-role ARN + role unique id) đối chiếu với registry do Security Operations quản lý. Session name không bao giờ là danh tính; body có trường danh tính bị từ chối. Từ chối IAM user, root, account khác, ngữ cảnh không nhất quán, role chưa đăng ký hoặc bị vô hiệu, registry hỏng. Operator và approver phải là hai người khác nhau | Xác minh IAM production thuộc C-EX2. Quy trình Security Ops quản lý registry chưa có (§9) |
| CF-02 Approval | **Tuân thủ ở mức code.** Approval gắn deployment, action, target, version, operator, `expires_at` (5 phút đến 4 giờ, mặc định 1 giờ) và operation id. Single-use; cấm tự phê duyệt, replay và dùng chéo deployment; có tombstone `issuance#`; không tự mở lại bootstrap | Hoàn tất (activate/complete) không bị giới hạn bởi hạn approval để không bỏ dở SQLite đã commit (giống reservation không hết hạn) |
| CF-03 Transition | **Tuân thủ ở mức code.** Hai handler và hai nhóm route riêng. Có ledger bền vững ghi cùng transaction. Ghi có điều kiện. Kết quả không rõ được báo là `OutcomeUnknown`, client thử lại cùng `request_id` rồi đọc lại. `ClientRequestToken` chỉ là lớp phụ. Không giả định DynamoDB và SQLite chung transaction | — |
| CF-04 Tương thích | **Tuân thủ.** Runtime vẫn chỉ đọc, fail-closed, không đổi định dạng bản ghi. S01-B (JWT, `token_version`, account status, audit) không đổi. Không có endpoint HTTP bootstrap, không admin mặc định, không LocalSeal ngoài Dev/Test | `Api_ExposesNoHttpBootstrapOrRecoveryEndpoint_AndContainsNoAuthorityMutationCode` được mở rộng để kiểm tra API không tham chiếu Authority Service hay CLI |
| CF-05 Phạm vi | **Tuân thủ.** Chỉ R1–R6 và C-IR-01..11. Không có migration và không đổi schema SQLite. Không có S01-D/E/F | Thay đổi ngoài S01-C: benchmark S01-B (chỉ test, C-IR-06) và 3 khóa metadata audit (C-IR-04) |

**REM-G01 đến REM-G10: NOT ASSESSABLE.** Định nghĩa các gate này không có trong repo hay trong prompt ủy quyền. Implementer không tự đặt nội dung cho chúng. Đề nghị PO công bố định nghĩa để reviewer độc lập đối chiếu với §3, §4 và §5.

## 6. Đánh giá lại C-G01 đến C-G09 (đề xuất của implementer, chờ reviewer)

| Gate | Đánh giá độc lập trước đây | Sau remediation |
|---|---|---|
| C-G01 | PASS | PASS (code/test) |
| C-G02 | PASS ở mức code, IAM chưa xác minh | PASS (code/test). Template runtime chỉ đọc đã có test âm. **IAM thật: NOT VERIFIED** |
| C-G03 | PASS | PASS. SQLite race được kiểm tra trực tiếp với reservation giả (`ConcurrentSqliteBootstrapCommits_WithForgedReservations_CommitExactlyOneAdmin`) |
| C-G04 | PASS | PASS. Thêm race 8 request đồng thời ở server và recovery đồng thời qua service |
| C-G05 | PASS ở mức code/test double | PASS (code/test double). Có kill tiến trình thật |
| C-G06 | CÓ ĐIỀU KIỆN (C-IR-01) | **Điều kiện C-IR-01 đã được gỡ ở mức code** (operator không mở lại được bootstrap; có tombstone). Restore snapshot `Completed` cùng `state_version` vẫn thuộc C-G09 |
| C-G07 | MỘT PHẦN | **Ủy quyền độc lập và danh tính audit đã có ở mức code.** Thủ tục nghiệp vụ C-R05 và AWS thật chưa có |
| C-G08 | MỘT PHẦN | 414/414 × 3 lần chạy cục bộ. **Vẫn không phải bằng chứng CI độc lập, cũng không phải bằng chứng AWS** |
| **C-G09** | OPEN | **OPEN, chặn production.** Remediation này không mở rộng phạm vi sang C-G09 |

## 7. Lệnh test và kết quả thực tế

Môi trường: .NET SDK 10.0.101, target net8.0, Windows 11 Pro, máy cục bộ (không phải CI).

| Lệnh | Kết quả |
|---|---|
| `dotnet build src/backend/InternalKnowledgeCopilot.sln --no-incremental` | Thành công, **0 warning, 0 error** |
| `dotnet test src/backend/InternalKnowledgeCopilot.sln` trên bản build cuối (lần 1) | **414 passed / 0 failed / 0 skipped** (1 phút 38 giây) |
| `dotnet test ... --no-build`, cùng binary (lần 2, lần 3) | **414/0/0** mỗi lần (1 phút 27 giây, 1 phút 24 giây) |
| Ba lần chạy trước đó trên bản build trung gian (chỉ khác bản cuối ở comment và việc làm sạch mã lý do trả về từ service) | **414/0/0** × 3 |
| `--filter FullyQualifiedName~S01C` | 149/0/0 |

**Tổng số test:** 414 = 265 (S01-A/B và trước đó, gồm benchmark S01-B đã chuyển class) + 149 (S01-C; trước remediation là 92).

Số test case S01-C theo class:

| Class | Số test case |
|---|---|
| `S01CAuthorityServiceTests` (mới) | 22 |
| `S01CIamPolicyTests` (mới) | 14 |
| `S01CRemediationCliTests` (mới) | 20 |
| `S01CProcessTerminationTests` (mới) | 2 |
| `S01CBootstrapTests` | 35 |
| `S01CDynamoDbAuthorityTests` (viết lại) | 31 |
| `S01CRestoreRecoveryTests` | 12 |
| `S01CConcurrencyTests` | 7 |
| `S01CHttpIntegrationTests` | 6 |

Các test mutator cũ (`Mutator_*`, `OperatorTransitions_*`) đã bị bỏ vì đường ghi trực tiếp của operator không còn tồn tại. Thuộc tính tương ứng được kiểm chứng ở server, trong `S01CAuthorityServiceTests`.

**Ma trận test bắt buộc theo prompt:**

| Yêu cầu | Test |
|---|---|
| Từ chối ghi trực tiếp của operator | `DeploymentOperator_DirectAuthorityWrite_IsDenied` (6), `AuthorityChanges_GoOnlyThroughAuthorityServiceConditionalTransactions`, `Api_..._ContainsNoAuthorityMutationCode` |
| Principal/role giả mạo | `ForgedOrUntrustedPrincipalContext_IsRejected` (6), `IdentityClaimsInRequestBody_AreRejected` (3), `RoleSeparation_*`, `MissingOrInvalidPrincipalRegistry_FailsClosed` |
| Tự phê duyệt qua session khác | `SelfApproval_IsForbidden_EvenThroughADifferentRoleOrSession` |
| Gọi Lambda trực tiếp trái phép | `DirectLambdaInvocation_WithoutIamAuthenticatedGatewayContext_IsRejectedBeforeAnyRead`, `AuthorityLambda_IsInvocableOnlyThroughTheIamAuthenticatedApi` |
| Approval hết hạn, đã dùng, replay | `Approval_IsBoundToAssignedOperatorExpiryAndIsSingleUse`, `IdempotentRetry_*`, `CrossDeploymentUse_IsRejected` |
| Đồng thời / version cũ | `StaleVersionAndConcurrentTransitions_AllowExactlyOneWinner`, `ConcurrentBootstrap_*`, `ConcurrentRecovery_*` (2 provider) |
| Kết quả không rõ / retry idempotent | `UnknownStoreOutcome_*`, `Bootstrap_ActivationAppliedThenTimeout_IsResolvedByLedgerReplayNotReapplied`, `LostServiceResponse_IsRetriedWithSameRequestIdAndNotReapplied`, `ServiceUnreachable_BeforeReserve_ChangesNothing` |
| Lệch commit SQLite / activate authority | `SqliteCommittedButActivationFailed_IsReconciledOnlyThroughIndependentApproval`, `Bootstrap_ProcessKilledAfterSqliteCommitBeforeActivation_*`, `Bootstrap_ActivationTimeoutAfterSqliteCommit_*` |
| Thu hồi token khi recovery | `AdminRecoveryThroughService_RevokesTokensAndConsumesApprovalOnce`, `EndToEnd_LocalSeal_*` (HTTP), `AdminRecovery_ProcessKilled*` |
| Thiếu danh tính trong audit | `MissingVerifiedOperatorIdentity_BlocksSqliteChanges`, `MissingVerifiedOperatorIdentityOnRecoveryClaim_*`, `AuditEvents_*` |
| Bỏ qua đặc quyền qua biến môi trường | `Cli_Mutation_RequiresElevatedOperatorInEveryEnvironmentBeforeAnyAuthorityAccess` (3), `TestHarness_Refuses*` (4) |
| Regression S01-A/B/C | Toàn bộ suite (414) |

**Giới hạn của bằng chứng:**
- Mock và fake **không chứng minh** AWS IAM, API Gateway, Lambda hay STS thực thi đúng.
- Bộ mô phỏng IAM chỉ đánh giá chính các template trong repo, theo ngữ nghĩa rút gọn.
- Các test `Cli_Mutation_RequiresElevated*` chỉ kiểm tra nhánh "không nâng quyền" khi tiến trình test không chạy với quyền admin.

## 8. Runbook recovery và reconciliation (thay thế §6 của tài liệu S01-C cho provider DynamoDb)

Luôn bắt đầu bằng `recovery-check` (chỉ đọc). Approver dùng route `POST /authority/approvals` bằng role Approver của chính mình. Operator dùng CLI trên host, trong phiên nâng quyền, với profile operator có MFA.

| Condition | Approver (người thứ nhất) | Operator được chỉ định (người thứ hai) |
|---|---|---|
| `NotAuthorized` | `Bootstrap` (`expected_authority_version = null`, `assigned_operator_person_id`, `expires_in_minutes`) | `bootstrap` |
| `EligibleForBootstrap` | — | `bootstrap` (hỏi MFA, mật khẩu, `BOOTSTRAP`) |
| `ReservationWithoutSqliteEvidence` | Điều tra (DB path, restore, C-G09). Nếu xác nhận không có bằng chứng SQLite: `ReauthorizeBootstrap` với version hiện tại | `bootstrap` |
| `BootstrapActivationPending` | `BootstrapReconciliation` với version hiện tại | `recover-admin`, gõ `RECOVER` |
| `RecoveryAuthorized` (cần recovery admin) | Từ `Activated`: `AdminRecovery` kèm `target_admin_id`; gửi email đích cho operator qua kênh đã duyệt | `recover-admin`, nhập email đích từ yêu cầu đã duyệt |
| `RecoveryAppliedPendingFinalization` | — | Chính operator đã nhận approval chạy lại `recover-admin`, gõ `FINALIZE` |
| Approval hết hạn chưa dùng | Cấp lại cùng action với version hiện tại; approval cũ thành `Superseded` | — |
| `AuthorityRevoked` | Không có route nào gỡ. Cần quyết định riêng của PO/Security Ops | — |
| Kết quả không rõ (`authority_service_timeout`/`unreachable`, exit 3 hoặc 4) | — | Chạy lại `recovery-check`. Không sửa bản ghi bằng tay. CLI đã tự thử lại với cùng `request_id`; nếu vẫn không rõ thì leo thang |
| `StateVersionMismatch`, `EvidenceMismatch`, `AnomalousSqliteState` (kể cả `schema_integrity_failed` do trigger) | Leo thang Security Operations | Không thao tác |

Mã từ chối của service mà operator có thể gặp:
- `operator_not_assigned`, `approval_expired`, `approval_not_current`, `approval_superseded`, `self_approval_forbidden`.
- `principal_not_registered`, `principal_disabled`, `deployment_not_permitted`, `authority_version_mismatch`, `request_id_conflict`.

## 9. Mục phụ thuộc AWS **CHƯA KIỂM CHỨNG** và rủi ro còn lại

1. **Hạ tầng (C-EX2):**
   - Adapter Lambda (API Gateway proxy event → `AuthorityInvocation`) **chưa được viết**; cần thêm package `Amazon.Lambda.*`.
   - Việc tạo API Gateway với IAM auth, Lambda, resource policy, bảng DynamoDB (PITR, deletion protection, KMS), CloudTrail data events và cảnh báo đều chưa làm.
2. **IAM thật:** các template chưa được nạp vào IAM. Chưa xác minh SCP, permission boundary, `dynamodb:LeadingKeys` với TransactWriteItems (`PutItem`/`ConditionCheckItem`), hay hành vi `execute-api:Invoke`.
3. **STS/MFA:** chưa chạy `AssumeRole` có MFA thật. Thời hạn session, `aws:MultiFactorAuthAge` và cách IdP/Identity Center phát hành danh tính theo người đều chưa kiểm chứng.
4. **DynamoDB:** chưa kiểm chứng `TransactWriteItems` và `CancellationReasons` thật, giới hạn kích thước item registry, độ trễ, throttling và tính nhất quán.
5. **Registry principal:** quy trình Security Operations tạo, thu hồi, xoay vòng mapping chưa có (C-R05). Service chỉ đọc registry.
6. **Công cụ cho approver:** chưa có client riêng. Mọi client SigV4 dùng được, nhưng cần quy trình và công cụ được duyệt.
7. **Hệ quả thiết kế cần PO xác nhận:**
   - `Revoked` không gỡ được bằng service.
   - Approval recovery đã nhận chỉ chính operator đó hoàn tất được, không có reassignment.
   - Hoàn tất không bị giới hạn bởi hạn approval.
   - Region của Authority Service được giả định trùng `DynamoDb:Region`.
   - CLI đọc authority bằng role runtime của host.
8. **LocalSeal:** vẫn là cơ chế chỉ dành cho Development/Testing, không có ranh giới bảo mật nào (đã nêu trong code và tài liệu).
9. **Tin cậy host:** operator có quyền admin trên host vẫn sửa được SQLite (C-R01, C-R04). Server ngăn giả mạo trạng thái authority, nhưng không ngăn được sửa SQLite.
10. **C-G09 / C-R01:** restore snapshot `Completed` cùng `state_version` vẫn không phát hiện được. **OPEN, chặn production.**

## 10. Gói cho independent security re-review

**Đề nghị:** cần một vòng **independent security re-review** trước C-EX2. Không merge PR này và PR #8 cho tới khi PO quyết định.

**Phạm vi cần đọc:**
- `src/backend/InternalKnowledgeCopilot.AuthorityService/` (toàn bộ, kèm `Policies/`).
- `src/backend/InternalKnowledgeCopilot.PlatformCli/Authority/`, `Bootstrap/PlatformBootstrapService.cs`, `Recovery/PlatformRecoveryService.cs`, `PlatformCliContext.cs`, `PlatformCliApplication.cs`, `Preflight/PlatformDatabaseInspector.cs`.
- `src/backend/InternalKnowledgeCopilot.PlatformCli.TestHarness/Program.cs`.
- `src/backend/InternalKnowledgeCopilot.Api/Modules/Platform/Provisioning/AuthorityServiceContracts.cs`, `Infrastructure/Options/*`, `Modules/Platform/Auth/PlatformAuditWriter.cs`.
- Test: `src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/*`, `Platform/PlatformPasswordHasherBenchmarkTests.cs`.

**Câu hỏi trọng tâm cho reviewer:**
1. Có còn đường nào để operator (hoặc approver đơn lẻ) đạt tới `Authorized`, `RecoveryPending`, `Activated` hay gỡ `Revoked` mà không cần người thứ hai không?
2. Việc xác định principal (assumed-role ARN + role unique id + registry) có đủ cho CF-01 không? Kiểm tra cả rủi ro role bị tạo lại và chia sẻ role.
3. Ngữ nghĩa ledger và retry: có kịch bản nào áp dụng hai lần hoặc tạo trạng thái treo không?
4. `ReauthorizeBootstrap` từ `Reserved` khi operator đang ở giữa operation: hệ thống có luôn fail-closed không?
5. Template IAM: `LeadingKeys` với TransactWriteItems, các deny tường minh, resource policy của Lambda.
6. Danh tính trong audit SQLite: có đủ cho AUD-01 không, và có rủi ro quyền riêng tư nào không?

**Cách tái tạo:** `dotnet build src/backend/InternalKnowledgeCopilot.sln --no-incremental` rồi `dotnet test src/backend/InternalKnowledgeCopilot.sln`. Không cần AWS.
