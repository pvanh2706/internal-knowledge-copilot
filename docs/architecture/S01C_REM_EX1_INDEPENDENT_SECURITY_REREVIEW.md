# Independent Security Re-Review: S01-C REM-EX1 (PR #9)

**Loại tài liệu:** Báo cáo re-review độc lập (kiến trúc, bảo mật, implementation), gửi PO đánh giá
**Ngày review:** 2026-10-31
**Người review:** Claude Code (Claude Opus 5.5). Xem giới hạn về tính độc lập ở §1.
**Trạng thái:** Báo cáo review, **không phải** quyết định acceptance. Báo cáo này **không** cho phép merge, provisioning AWS hay deploy production. **C-G09 vẫn OPEN và chặn production.**

> Review read-only trên HEAD `10be150`. Trong lúc review không sửa file trong repo, không commit/push, không gọi AWS. Thao tác mạng duy nhất là `git ls-remote` (đọc ref trên GitHub). Thí nghiệm bổ sung chạy ngoài repo, trên DLL build từ chính HEAD này. File báo cáo này được commit sau đó theo yêu cầu riêng của người dùng; commit đó chỉ thêm tài liệu, không thay đổi code đã review.

---

## 1. GitHub refs và SHA đã review

| Mục | Giá trị | Kiểm chứng |
|---|---|---|
| Repository | `pvanh2706/internal-knowledge-copilot` | `git remote -v` |
| PR | #9 (stacked trên PR #8), branch `feature/s01-c-security-remediation` | |
| Base SHA | `7101d843e0017d54c4ca73fe5d455a4510c3f1ca` | Khớp Expected base, `refs/heads/feature/s01-c-bootstrap-recovery` và `refs/pull/8/head` |
| HEAD đã review | `10be1501b7cd8395d2be00b5cd526250a5f1dd74` | Khớp Expected HEAD, `refs/pull/9/head` và HEAD cục bộ |
| `main` | `9b9bf5bad7733ecb7eef8e7a8d9cf60ef2fcb3b3` | PR #8 và PR #9 đều chưa merge |
| Quy mô | 4 commit, 53 file, +4904/−668 | `git diff --stat 7101d84..10be150` |
| Cách làm | Đọc tĩnh toàn bộ diff và các file liên quan. Build và chạy test 2 lần. 5 thí nghiệm ngoài repo (§8.3). | |
| Trạng thái working tree | Sạch trước và sau review | `git status` |

**Giới hạn về tính độc lập.** Cả 4 commit của PR #9 có trailer `Co-Authored-By: Claude Opus 5.5`. Re-review này cũng do Claude Opus 5.5 thực hiện, trong một phiên riêng và không dựa trên kết luận của implementer. Vì vậy review độc lập về **phiên làm việc**, nhưng **không** độc lập về tổ chức hay về mô hình. Khuyến nghị có thêm một reviewer bảo mật là con người trước khi ký REM-G09.

**Tài liệu baseline:**

| Tài liệu | Có trong repo? | Ảnh hưởng tới review |
|---|---|---|
| S01 Technical Design Baseline v1.0, DEC-0008 | Có | Đối chiếu trực tiếp |
| PR #8 và báo cáo review độc lập (C-IR-01–C-IR-11) | Có: [S01C_INDEPENDENT_SECURITY_REVIEW.md](S01C_INDEPENDENT_SECURITY_REVIEW.md) | Đối chiếu trực tiếp |
| Báo cáo remediation PR #9 | Có: [S01C_SECURITY_REMEDIATION_R1_R6.md](S01C_SECURITY_REMEDIATION_R1_R6.md) | Đối chiếu trực tiếp |
| S01-C-ADR-01, Detailed Security Design v1.0, Remediation Plan R1–R6 v1.0 | **Không.** `docs/decisions/` chỉ có tới DEC-0008. | **Evidence gap.** Đánh giá dựa trên nội dung được trích lại trong prompt và báo cáo remediation. |
| Contract Freeze CF-01–CF-05 | Chỉ có bản tóm tắt do implementer trích lại (§5 báo cáo remediation) | **Evidence gap.** Không có văn bản gốc đã duyệt. |
| REM-G01–REM-G10 | Chỉ có ý nghĩa một dòng trong prompt re-review | **Evidence gap.** Gate được chấm theo ý nghĩa đó. |

Các yêu cầu trên **không** bị coi là không tồn tại chỉ vì văn bản không có trong repo. Tuy vậy, mọi kết luận về CF và REM-G cần được đối chiếu lại khi PO công bố văn bản đã duyệt.

## 2. Tóm tắt

- **Hướng kiến trúc của ADR-01 là đúng, và phần lớn đã được hiện thực đúng ở mức code.**
  - Operator không còn đường ghi DynamoDB nào: mutator đã bị xóa và CLI không có client ghi.
  - Sơ đồ chuyển trạng thái, approval single-use, tách nhiệm vụ, ledger bền vững và ghi có điều kiện đều được cưỡng chế **ở server**.
  - Server tự tính `state_version` và `bootstrap_admin_id`.
  - Có tombstone `issuance#`. Không có route nào gỡ `Revoked`.
  - Tự phê duyệt bị chặn bằng `person_id` ở **cả hai** handler.
  - **Đường tấn công gốc của C-IR-01 (operator gọi `UpdateItem` tạo trạng thái bất kỳ) đã bị loại bỏ.**
- **Phát hiện chính mới, C-RR-01 (P1): ranh giới invocation chưa được bảo vệ.**
  - Danh tính lấy từ các trường `requestContext` mà service **không tự xác thực được**.
  - Bất kỳ principal nào gọi trực tiếp được Lambda, hoặc sửa được integration của API Gateway hay code/config của Lambda, đều mạo danh được **cả approver lẫn operator**. Như vậy cả hai nửa của nguyên tắc hai người bị phá cùng lúc.
  - Resource policy của Lambda không ngăn được việc này: trong cùng một account, chỉ cần identity policy cho phép là đủ.
  - Test `AuthorityLambda_IsInvocableOnlyThroughTheIamAuthenticatedApi` khẳng định quá mức, cùng kiểu với C-IR-05.
  - Thí nghiệm E3 xác nhận: context giả, không có SigV4, vẫn được `Applied`.
- **C-RR-02 (P2): thu hồi principal trong registry không có hiệu lực với request đang chạy.**
  - Commit không kiểm tra version của registry. Thí nghiệm E1 xác nhận.
  - Approval do một approver **sau đó** bị vô hiệu vẫn dùng được (E2). Điểm này cần PO quyết định chính sách.
- **C-RR-03 (P2): TCB và template IAM chưa đủ để làm đầu vào cho C-EX2.**
  - Không có resource policy cho bảng, không có SCP hay permission boundary.
  - Không có template cho người ghi `registry#principals`.
  - Execution role thiếu quyền ghi log.
  - Trust policy chưa ràng buộc chắc chắn "một người một role".
- **C-RR-04 (P2, availability): có các trạng thái ngõ cụt.** Chúng đều fail-closed nhưng không có đường ra trong protocol, ngoài `Revoke` không thể gỡ.
- **P3:** phân loại "kết quả không rõ" chưa đúng CF-03 (E4, E5); bằng chứng test (IAM simulator, SigV4, kill process); tính bền vững và chống sửa của audit; governance.
- **Không tìm thấy đường nào khiến replay, timeout hay recovery đồng thời áp dụng lại thay đổi đặc quyền.**
- **Test:** build `--no-incremental` cho 0 warning và 0 error. `dotnet test` đạt **414/414 ở cả 2 lần chạy**, trong đó 149 test S01-C. Claim 414/414 **tái tạo được**. Đây không phải bằng chứng CI và không phải bằng chứng AWS.
- **REM-G:**
  - PASS (code): G01, G05, G07, G10.
  - PARTIAL: G02, G04, G06, G08, G09.
  - **FAIL: G03.**
- **Kết luận: REMEDIATION REQUIRED**, ở dạng một vòng nhỏ có mục tiêu. Không phải BLOCKED (xem §11).

## 3. Phát hiện

**Thang mức độ** (giữ như review trước):
- **P0:** khai thác được trên code đã ship, hoặc phá ngay một invariant cốt lõi.
- **P1:** vô hiệu hóa một thuộc tính bảo mật bắt buộc của thiết kế production.
- **P2:** khiếm khuyết có giới hạn, hoặc lỗi nhưng vẫn fail-closed.
- **P3:** hygiene, chất lượng bằng chứng, tài liệu.

ID mới dùng tiền tố **C-RR-xx** (Re-Review).

| ID | Mức | Loại | Tóm tắt |
|---|---|---|---|
| — | **P0** | — | **Không có.** |
| **C-RR-01** | **P1** | CONFIRMED (E3 + ngữ nghĩa đánh giá policy của AWS) | Ranh giới invocation không được bảo vệ: context gateway giả được tin là danh tính. |
| C-RR-02 | P2 | CONFIRMED (E1). E2 là khoảng trống chính sách. | Thu hồi trong registry không nguyên tử với commit. Approval của approver đã bị thu hồi vẫn dùng được. |
| C-RR-03 | P2 | Thiết kế / NOT VERIFIED | TCB và template IAM chưa đầy đủ (bảng, SCP, người ghi registry, log, trust policy). |
| C-RR-04 | P2 | Phân tích tĩnh. Rủi ro cần quyết định chính sách. | Trạng thái ngõ cụt fail-closed, không có đường ra ngoài `Revoke`. |
| C-RR-05 | P3 | CONFIRMED (E4, E5) | Phân loại kết quả không rõ chưa đúng CF-03. |
| C-RR-06 | P3 | CONFIRMED | Bằng chứng test nói quá hoặc mang tính vòng tròn; thiếu test âm. |
| C-RR-07 | P3 | Phân tích tĩnh | Audit: ledger bền vững nhưng có thể bị sửa bởi role của service. Danh tính trong SQLite chỉ là giá trị do service báo về. |
| C-RR-08 | P3 (chặn REM-G09) | Governance | Văn bản nguồn chưa công bố, quyết định thiết kế chưa được duyệt, tính độc lập của review bị giới hạn. |

### C-RR-01 (P1): Ranh giới invocation không được bảo vệ

**Bằng chứng**

- [TrustedPrincipal.cs:58-118](../../src/backend/InternalKnowledgeCopilot.AuthorityService/TrustedPrincipal.cs#L58-L118) chỉ **so sánh chuỗi** trên `ApiId`, `Stage`, `AuthorizationType`, `AccountId`, `UserArn` và `Caller` của `GatewayRequestContext`. Không có thành phần nào được ký hay được xác minh độc lập phía server.
- Comment "lời gọi Lambda trực tiếp dừng ở đây" ở [AuthorityServiceEndpoint.cs:27-33](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityServiceEndpoint.cs#L27-L33) chỉ đúng khi context **thiếu hoặc sai**.
- [authority-lambda-resource-policy.json](../../src/backend/InternalKnowledgeCopilot.AuthorityService/Policies/authority-lambda-resource-policy.json) chỉ có một câu lệnh Allow cho `apigateway.amazonaws.com`.
  - Theo logic đánh giá policy của AWS, với request trong cùng account, chỉ cần identity-based policy cho phép `lambda:InvokeFunction` là đủ. Resource policy không thu hẹp được quyền này.
  - Theo hiểu biết của reviewer, Lambda function policy được quản lý qua `AddPermission` và không có câu lệnh Deny. Điểm này cần xác minh trong C-EX2.
- [S01CIamPolicyTests.cs:103-125](../../src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/S01CIamPolicyTests.cs#L103-L125) đánh giá **riêng** resource policy rồi kết luận "operator gọi trực tiếp ⇒ ImplicitDeny". Tên test khẳng định "chỉ invoke được qua API". Điều này không đúng với AWS, và lặp lại đúng kiểu lỗi C-IR-05.
- [S01CAuthorityServiceTests.cs:17-40](../../src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/S01CAuthorityServiceTests.cs#L17-L40) chỉ thử context null hoặc sai trường. Mọi test khác đều tự dựng một context hợp lệ bằng `ContextFor(...)`, và đó chính là việc một kẻ gọi trực tiếp làm được.
- **Thí nghiệm E3:** gọi `HandleAsync` với context "bob/approver" do test tự dựng, không có SigV4. Kết quả: `http=200 outcome=Applied reason=approval_issued`, bản ghi chuyển sang `Authorized`.

**Kịch bản lạm dụng**

1. Một principal trong tài khoản authority có `lambda:InvokeFunction` trên function. Ví dụ: admin, role CI/CD, developer Lambda.
2. Principal đó dựng event với ARN và role unique id của approver, rồi của operator. Hai giá trị này không phải bí mật: lấy được qua `iam:GetRole` hoặc CloudTrail.
3. Một mình principal đó đi hết `Bootstrap → reserve → activate`, hoặc `AdminRecovery → claim-recovery → complete-recovery`.
4. Biến thể với cùng hệ quả:
   - Có quyền sửa integration hay mapping template của API Gateway: chèn context tùy ý vào event.
   - Có quyền sửa code hay cấu hình Lambda: bỏ qua toàn bộ logic.

**Phạm vi.**
- Template operator và approver đã Deny tường minh `lambda:InvokeFunction` ([deployment-operator.json:16-26](../../src/backend/InternalKnowledgeCopilot.AuthorityService/Policies/deployment-operator.json#L16-L26), [security-approver.json:11-21](../../src/backend/InternalKnowledgeCopilot.AuthorityService/Policies/security-approver.json#L11-L21)). Nếu hai role này chỉ mang đúng template đó thì bản thân chúng không làm được việc trên.
- Rủi ro nằm ở **các principal khác** và ở các quyền quản trị. Hiện chưa có control nào ràng buộc nhóm này.

**Vì sao là P1.** Chưa có gì được deploy để khai thác. Tuy nhiên, thuộc tính bắt buộc "protected invocation boundary" (REM-G03) chưa được thiết lập, còn bằng chứng và tài liệu R3 lại khẳng định là đã có.

**Khắc phục** (cần PO chọn):
- **(a) Bắt buộc, thuộc C-EX2:**
  - Tài khoản AWS riêng cho authority.
  - SCP hoặc permission boundary Deny `lambda:InvokeFunction`, `lambda:Update*`, `lambda:AddPermission`, `lambda:CreateFunctionUrlConfig` và các thao tác ghi `apigateway:*` trên tài nguyên authority, cho mọi principal trừ pipeline và break-glass đã được duyệt.
  - Service principal của API Gateway không chịu ảnh hưởng của SCP.
- **(b) Khuyến nghị thêm: bằng chứng danh tính đầu-cuối.**
  - Client gửi kèm một request `sts:GetCallerIdentity` đã ký sẵn (presigned), gắn với hash của body, `request_id` và thời hạn ngắn.
  - Lambda tự gửi request đó tới STS và nhận lại ARN/UserId đã được AWS xác thực. Đây là mẫu IAM auth của HashiCorp Vault.
  - Khi đó một context gateway giả không còn đủ để mạo danh.
  - Phương án này cần code mới và một quyết định thiết kế.
- **(c) Adapter Lambda:**
  - Chỉ nhận đúng một schema đã chọn. REST API (v1) có `requestContext.identity`, còn HTTP API (v2) dùng `requestContext.authorizer.iam`; cần chốt một loại.
  - Từ chối stage `test-invoke-stage`.
- **(d)** Sửa tên và khẳng định của test, và tài liệu R3 §2/§3.

**Gate:** cần quyết định của PO **trước khi freeze thiết kế C-EX2**.

### C-RR-02 (P2): Thu hồi principal không nguyên tử với commit

- Registry chỉ được đọc một lần, ở [AuthorityServiceEndpoint.cs:35-53](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityServiceEndpoint.cs#L35-L53).
  - Commit ở [AuthorityServiceEndpoint.cs:145-148](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityServiceEndpoint.cs#L145-L148) chỉ gồm các writes và checks do handler trả về.
  - Không handler nào thêm `AuthorityStoreCheck` cho `registry#principals`. Check duy nhất là trên bản ghi deployment, ở [AuthorityTransitionHandler.cs:175](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityTransitionHandler.cs#L175) và [:191](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityTransitionHandler.cs#L191).
- **E1:**
  - Security Ops đặt `alice` thành `Active=false` sau khi danh tính đã được resolve nhưng trước khi commit.
  - `reserve` của alice vẫn trả `Applied` và bản ghi chuyển sang `Reserved`.
  - Chỉ request **tiếp theo** mới bị từ chối với `principal_disabled`.
- **Cửa sổ rủi ro:** từ lúc đọc registry tới lúc `TransactWriteItems`, gồm 3–5 lần đọc consistent và một transaction. Thường là mili-giây, tối đa bằng timeout của store hoặc Lambda. Khai thác cần trùng thời điểm nên khả năng thấp, nhưng đây là khiếm khuyết **đã xác nhận ở mức code**.
- **Khoảng trống chính sách (E2):**
  - Approval do `bob` cấp vẫn được alice tiêu thụ sau khi bob bị vô hiệu: `Applied`, bản ghi `Reserved`.
  - Lý do: `Reserve` và `ClaimRecovery` không kiểm tra lại người phê duyệt.
  - Ngoài việc chờ approval hết hạn hoặc `Revoke` deployment, hiện không có cách nào hủy một approval đang `Issued`.
- **Khắc phục:**
  - Thêm `ConditionCheck` trên version của registry vào **mọi** commit. Cái giá là request đang chạy sẽ bị hủy khi registry thay đổi, điều này chấp nhận được.
  - Nếu PO quyết định, kiểm tra lại `ApprovedByPersonId` vẫn active ở `reserve` và `claim-recovery`.
  - Thêm test cho cả hai trường hợp.

### C-RR-03 (P2): TCB và template IAM chưa đầy đủ

- **Bảng DynamoDB không có resource-based policy.** Tính chất "chỉ service ghi" vì vậy dựa vào giả định rằng không principal nào khác trong account có quyền ghi.
- **Không có template hay quy trình cho principal ghi `registry#principals`.**
  - Người ghi registry kiểm soát gốc của danh tính: có thể ánh xạ role của chính mình sang một `person_id` bất kỳ, và như vậy vượt qua nguyên tắc hai người.
  - Cần kiểm soát hai người và audit riêng cho việc ghi registry.
- **Danh sách Deny chưa đầy đủ nếu được coi là một bảo đảm.**
  - Ví dụ còn thiếu: `dynamodb:PutResourcePolicy`, `dynamodb:DeleteResourcePolicy`, `dynamodb:UpdateContinuousBackups`, `dynamodb:RestoreTableFromBackup`, `dynamodb:ImportTable`, `lambda:UpdateAlias`, `lambda:PublishVersion`, `lambda:CreateFunctionUrlConfig`, `lambda:PutFunctionConcurrency`, các thao tác ghi `apigateway:*`.
  - Operator và approver không có câu Allow nào cho các hành động này, nên implicit deny vẫn áp dụng. Đây là vấn đề drift và phòng thủ chiều sâu, không phải lỗ hổng trực tiếp.
  - Khuyến nghị Deny theo resource ARN của authority, dùng `NotAction`.
- **Execution role** ([authority-service-execution-role.json](../../src/backend/InternalKnowledgeCopilot.AuthorityService/Policies/authority-service-execution-role.json)) không có quyền CloudWatch Logs. Nếu không bổ sung, các sự kiện audit ghi ra stdout sẽ bị mất. Cũng chưa có log group, retention hay KMS.
- **Trust policy** ([per-person-role-trust-policy.json](../../src/backend/InternalKnowledgeCopilot.AuthorityService/Policies/per-person-role-trust-policy.json)):
  - `${PersonPrincipalArn}` không bị ràng buộc phải là một IAM user cụ thể. Nếu đặt thành `:root` hoặc một role SSO dùng chung thì role không còn là "của một người".
  - Không yêu cầu `sts:SourceIdentity`.
  - Điều kiện MFA chỉ có ý nghĩa khi người gọi là IAM user có MFA. Nếu dùng Identity Center thì cần thiết kế khác; CLI hiện đã chặn SSO.
- **Thiếu resource policy cho API Gateway** để chỉ cho phép `aws:PrincipalArn` của các role đã đăng ký.
- **Hygiene:** comment ở [TrustedPrincipal.cs:105](../../src/backend/InternalKnowledgeCopilot.AuthorityService/TrustedPrincipal.cs#L105) viết rằng role unique id "không đổi khi xóa rồi tạo lại". Điều này sai: AWS cấp unique id **mới**, và đó chính là lý do bind theo unique id là đúng. Logic code vẫn fail-closed.

### C-RR-04 (P2, availability): Trạng thái ngõ cụt

Tất cả các trường hợp dưới đây đều fail-closed nên **không phải lỗ hổng bảo mật**, nhưng không có đường ra trong protocol.

**(a) `ReauthorizeBootstrap` từ `Reserved` khi operator vẫn đang chạy**
- Server không yêu cầu reservation phải đủ "cũ" hay có xác nhận của operator ([AuthorityApprovalHandler.cs:106-141](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityApprovalHandler.cs#L106-L141), [:258-261](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityApprovalHandler.cs#L258-L261)).
- CLI xác nhận reservation "của mình" bằng consistent read rồi mới commit SQLite ([PlatformBootstrapService.cs:90-117](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Bootstrap/PlatformBootstrapService.cs#L90-L117)). Hai bước này không nguyên tử.
- Kết quả: bản ghi `Authorized` trong khi SQLite đã `Completed`, chẩn đoán ra `sqlite_completed_without_reservation` kèm hướng dẫn "do not re-authorize" ([ProvisioningReconciliation.cs:118-122](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Preflight/ProvisioningReconciliation.cs#L118-L122)).
- `BootstrapReconciliation` chỉ đi từ `Reserved`, nên không còn đường reconcile.

**(b) Approval recovery đã được claim**
- Chỉ đúng người đã claim mới hoàn tất được ([AuthorityTransitionHandler.cs:303-312](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityTransitionHandler.cs#L303-L312)). Không có chuyển giao, không có hạn, không có route hủy `RecoveryPending`.
- Nếu operator đó nghỉ việc hoặc bị vô hiệu, deployment kẹt ở `RecoveryPending`, trong khi runtime từ chối mọi platform auth. Lối ra duy nhất là `Revoke`.

**(c) Operator đã được phê duyệt gọi API trực tiếp, bỏ qua CLI**
- Operator `reserve` rồi `activate` mà không ghi SQLite, dẫn tới `Activated` với một admin không tồn tại.
- Việc này nằm trong quyền đã được phê duyệt và vẫn fail-closed, nhưng deployment bị kẹt.

**(d) Một approver đơn lẻ có thể `Revoke` vĩnh viễn**
- Xem [AuthorityApprovalHandler.cs:208-239](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityApprovalHandler.cs#L208-L239). Đây là rủi ro availability, liên quan C-R03.

**Cần PO quyết định:**
- Thời gian chờ tối thiểu, hoặc yêu cầu operator xác nhận đã dừng, trước khi được `ReauthorizeBootstrap`.
- Một action để reconcile reservation đã bị thay thế.
- Hạn giữ approval, cùng một đường hủy có hai người.
- Quy trình break-glass cho `Revoked`.

### C-RR-05 (P3): Phân loại kết quả không rõ chưa đúng CF-03

- [AuthorityStore.cs:120-123](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityStore.cs#L120-L123) coi mọi lỗi 4xx (trừ throttling) là "chắc chắn chưa ghi" (`OutcomeUnknown=false`).
  - Nhóm này gồm cả `TransactionInProgressException`: một transaction khác với cùng `ClientRequestToken` vẫn đang chạy và **có thể commit sau đó**.
  - **E4:** endpoint trả `503 Rejected authority_service_error`, thay vì `OutcomeUnknown`.
- **E5:** hai lần gửi cùng `request_id` chạy chồng nhau. Một lần nhận `Applied`; lần kia nhận `409 Rejected authority_concurrent_modification` thay vì `Replayed` ([AuthorityServiceEndpoint.cs:160-163](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityServiceEndpoint.cs#L160-L163)).
- **Không có áp dụng hai lần.** CLI luôn đọc lại authority nên vẫn fail-closed, nhưng có thể báo `Denied`/`Reconcile` cho một thao tác thực ra đã được áp dụng.
- **Khắc phục:**
  - Xếp `TransactionInProgress` và `IdempotentParameterMismatch` vào nhóm "không rõ".
  - Khi điều kiện của item ledger thất bại, đọc lại ledger và trả `Replayed` hoặc `request_id_conflict`.
  - Cho `FakeDynamoDb` mô phỏng các lỗi này.

### C-RR-06 (P3): Chất lượng bằng chứng test

- **IAM simulator** ([S01CIamPolicyTests.cs:233-323](../../src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/S01CIamPolicyTests.cs#L233-L323)):
  - Đánh giá từng policy riêng lẻ: không kết hợp identity với resource policy, không có SCP hay boundary.
  - `ForAllValues:*` trả `false` khi thiếu key ([:295-296](../../src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/S01CIamPolicyTests.cs#L295-L296)), trong khi AWS trả `true`.
  - Chỉ nên coi đây là lint cho template, không phải bằng chứng IAM.
- **SigV4:**
  - Chỉ có một vector chính thức, `get-vanilla` (GET, không body, không session token), ở [S01CRemediationCliTests.cs:17-30](../../src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/S01CRemediationCliTests.cs#L17-L30).
  - Gateway giả xác thực bằng cách **ký lại bằng chính `SigV4Signer`** ([AuthorityServiceTestHost.cs:193-215](../../src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/AuthorityServiceTestHost.cs#L193-L215)), tức là bằng chứng vòng tròn.
  - Nhánh POST có body và có `x-amz-security-token` chưa được đối chiếu với một implementation độc lập.
- **Kill process thật** chỉ có với LocalSeal ([S01CProcessTerminationTests.cs:11-13](../../src/backend/InternalKnowledgeCopilot.Tests/Platform/S01C/S01CProcessTerminationTests.cs#L11-L13)). Nhánh service/DynamoDB chỉ có exception hoặc timeout trong cùng tiến trình.
- **Thiếu test âm cho:**
  - Registry thay đổi giữa chừng một request.
  - Approver bị thu hồi sau khi đã cấp approval.
  - Race của `ReauthorizeBootstrap`.
  - Chuyển role sang người khác.
  - `TransactionInProgress`.
  - Context hợp lệ nhưng bị giả. Trường hợp này không chặn được bằng code, cần ghi rõ là phụ thuộc IAM.
- **Elevation:** `Cli_Mutation_RequiresElevated*` chỉ kiểm tra nhánh không nâng quyền. Implementer đã nêu điểm này.

### C-RR-07 (P3): Tính bền vững và chống sửa của audit

- **Điểm tốt:** ledger và approval được ghi **cùng transaction** với thay đổi, mang `person_id` và ARN role. Mọi thay đổi đã áp dụng đều truy vết được.
- **Ledger không chống sửa được trước role của service:** execution role có `PutItem` không điều kiện ở mức IAM trên `ledger#*` và `approval#*` ([authority-service-execution-role.json:5-12](../../src/backend/InternalKnowledgeCopilot.AuthorityService/Policies/authority-service-execution-role.json#L5-L12)). Không có hash chain, cũng không có bản sao append-only (ví dụ DynamoDB Streams → S3 Object Lock).
- **Sự kiện bị từ chối chỉ nằm trong log.** Chúng chỉ được ghi ra stdout ([AuthorityAudit.cs:59-78](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityAudit.cs#L59-L78)); độ bền phụ thuộc CloudWatch, mà phần này chưa có template (xem C-RR-03).
- **Danh tính trong audit SQLite chỉ là giá trị do service báo về.**
  - `operator_person_id` lấy từ phản hồi của service qua TLS ([AuthorityServiceHttpClient.cs:229-237](../../src/backend/InternalKnowledgeCopilot.PlatformCli/Authority/AuthorityServiceHttpClient.cs#L229-L237)).
  - Admin của host có thể trỏ endpoint sang một server giả, nên giá trị này không chống được giả mạo từ phía host.
  - Bằng chứng có thẩm quyền là ledger DynamoDB và CloudTrail. Khi ánh xạ vào AUD-01 cần ghi rõ mức đảm bảo này.
- **Sự kiện giả mạo theo C-RR-01 sẽ được ghi dưới tên người bị mạo danh.**

### C-RR-08 (P3, chặn REM-G09): Governance

- ADR-01, Detailed Security Design v1.0, Remediation Plan v1.0, văn bản gốc CF-01–CF-05 và REM-G01–G10 không có trong repo. `PROJECT_STATUS.md` đã lỗi thời. C-IR-11 vẫn OPEN.
- Các quyết định do implementer tự chọn chưa được PO phê duyệt chính thức: route, mô hình item, `Revoked` không gỡ được, không chuyển giao approval, hoàn tất không bị giới hạn bởi hạn approval, giả định region, và CLI đọc authority bằng role runtime.
- Tính độc lập của review bị giới hạn (§1).

### 3.1 Trả lời Priority 1 (C-IR-01)

| Câu hỏi | Kết luận | Bằng chứng / ghi chú |
|---|---|---|
| Operator không ghi DynamoDB trực tiếp | **PASS (code + template)** | `ProvisioningAuthorityMutators.cs` đã bị xóa. Không còn `UpdateItem`/`PutItem`/`DeleteItem` nào trong CLI hay service (grep). Template operator Deny ghi; role runtime chỉ có `GetItem`. AWS: NOT VERIFIED. Principal khác: C-RR-03. |
| Operator không tạo được `Authorized`/`RecoveryPending` | **PASS (code)** | Chỉ Approval Handler tạo hai trạng thái này ([AuthorityApprovalHandler.cs:86-94, 159-167](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityApprovalHandler.cs#L86-L167)). Kiểm tra route và chức năng ở hai lớp ([Endpoint:62-66](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityServiceEndpoint.cs#L62-L66), [Transition:20-23](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityTransitionHandler.cs#L20-L23)). Vẫn chịu ảnh hưởng của C-RR-01. |
| Operator không gỡ được `Revoked` | **PASS (code)** | Không có cạnh nào đi ra từ `Revoked`: `reserve`/`activate`/`complete` lần lượt đòi `Authorized`/`Reserved`/`RecoveryPending`; mọi action phê duyệt đều từ chối. Test `OperatorRoutes_CannotForge...` phủ mọi route. |
| Approver không thực hiện được thao tác của operator | **PASS (code)** | Có kiểm tra chức năng ở cả hai lớp, template Deny route operation, và test `RoleSeparation_*`. |
| Phê duyệt độc lập, chặn tự phê duyệt | **PASS (code)** | So `person_id` ở Approval Handler ([:45-48](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityApprovalHandler.cs#L45-L48)) và Transition Handler ([:59-62](../../src/backend/InternalKnowledgeCopilot.AuthorityService/AuthorityTransitionHandler.cs#L59-L62)). Operator được chỉ định phải active trong registry. Điều kiện tiên quyết: mỗi người thật chỉ có một `person_id` (governance). |
| Ánh xạ role ARN và role unique id | **PASS (code)** | Tra theo unique id (duy nhất trong registry), đối chiếu thêm tên role và account. Comment sai ở dòng 105 (C-RR-03). Định dạng trường thật của API Gateway: NOT VERIFIED. |
| Danh tính người tin cậy được | **PARTIAL** | `person_id` chỉ lấy từ registry. Body có trường lạ bị từ chối. Session name không dùng làm danh tính. Còn tồn tại: C-RR-02, người ghi registry chưa được kiểm soát, trust policy chưa ràng buộc theo người, chưa có `SourceIdentity` (C-RR-03). |
| Context gateway giả, gọi Lambda trực tiếp | **FAIL (thiết kế/bằng chứng)** | C-RR-01, thí nghiệm E3. |
| Chuyển role, chia sẻ credential, principal bị chiếm | **PARTIAL / cần chính sách** | **Chuyển role:** nếu registry đổi `person_id` của một role mà trust policy vẫn cho người cũ assume, thì người cũ hành động với danh tính người mới. Cần quy tắc "không bao giờ chuyển role". **Chia sẻ credential:** service không phát hiện được. **Một principal bị chiếm:** operator đơn lẻ không tạo được trạng thái authority; approver đơn lẻ không thực thi được nhưng có thể `Revoke` vĩnh viễn hoặc làm gián đoạn (C-RR-04). Principal có quyền invoke Lambda, quản trị API hoặc ghi registry thì vượt qua toàn bộ (C-RR-01, C-RR-03). |
| Tách quyền Approval Handler / Transition Handler | **PARTIAL** | Tách logic: 2 class, 2 nhóm route, kiểm tra chức năng 2 lần, IAM route riêng. **Không** tách vật lý: cùng tiến trình, cùng execution role, cùng quyền ghi. Nếu ADR-01 yêu cầu execution identity riêng thì chưa đạt; không xác định được vì thiếu văn bản ADR. |

**Thiếu adapter Lambda production, đánh giá riêng.** Tính đúng của các handler ở mức code không phụ thuộc vào adapter. Tuy nhiên, adapter là nơi `requestContext` được ánh xạ, tức là mắt xích quan trọng nhất của REM-G03, và nó **chưa tồn tại**. Vì vậy REM-G03 không thể PASS bằng bằng chứng hiện có, kể cả khi bỏ qua C-RR-01.

### 3.2 Trả lời Priority 2 (state machine và đồng thời)

| Hạng mục | Kết luận |
|---|---|
| Chuyển trạng thái bootstrap/recovery | **PASS (code).** Server tự tính `state_version` (bootstrap = 1; AdminRecovery = trước + 1; BootstrapReconciliation = 2) và `bootstrap_admin_id = reserved_admin_id`. Mọi bản ghi kết quả đều qua `Validate()`. |
| Cấp và tiêu thụ approval | **PASS (code).** `Issued → Consumed → Completed`/`Superseded`, có điều kiện version. Single-use được bảo đảm nhờ trạng thái approval cộng với liên kết `authorization_id`/`reservation_id`/`recovery_id`. Kiểm tra hạn ở bước claim. Bước hoàn tất không có hạn: đây là lựa chọn thiết kế, cần PO xác nhận. |
| Tombstone cấp phát bất biến | **PASS (code).** Service không xóa; IAM Deny `DeleteItem`; `Bootstrap` đòi không có `issuance#`. Không bất biến trước quản trị bảng hay restore (C-RR-03). |
| Ledger bền vững, chống replay | **PASS (code).** Ledger ghi cùng transaction. Fingerprint tính trên route và body đã chuẩn hóa, gắn với người gọi. Lệch nội dung hoặc lệch người cho `409`. P3: C-RR-05 (E5). |
| Transaction có điều kiện | **PASS (code).** `Put` với `attribute_not_exists` hoặc `version = expected`; `BuildRequest` buộc version mới = version cũ + 1; có `ConditionCheck`. DynamoDB thật: NOT VERIFIED. |
| Version và fingerprint | **PASS (code).** |
| Approval/transition đồng thời | **PASS (code/fake).** Race 8 request: đúng 1 bên thắng. Hai `Bootstrap` đồng thời đều dùng `attribute_not_exists` trên cả bản ghi lẫn tombstone. |
| Reauthorize từ `Reserved` | **Fail-closed về bảo mật**, nhưng có ngõ cụt (C-RR-04a). |
| Kết quả không rõ, mất phản hồi | **Phần lớn PASS.** Có retry cùng `request_id`, đọc lại authority, có `OutcomeUnknown`. Phân loại sai: C-RR-05. |
| Transaction SQLite và reconciliation | **PASS.** `BEGIN IMMEDIATE` không đổi; điều kiện được kiểm tra lại bên trong transaction; không ghi SQLite nếu chưa có danh tính đã xác minh. |
| Recovery idempotent, vô hiệu token | **PASS.** Claim idempotent theo người. `IsRecoveryAppliedAsync` và `state_version` chặn áp dụng lại. `token_version + 1` đúng một lần. `FINALIZE` không áp dụng lại credential. |
| **Thu hồi principal giữa lúc xác minh và lúc commit** | **CONFIRMED defect (C-RR-02, E1).** Cửa sổ hẹp. Approver bị thu hồi sau khi đã cấp approval là khoảng trống chính sách (E2). |

**Phân loại:**
- **Defect đã xác nhận:** C-RR-01 (E3), C-RR-02/E1, C-RR-05 (E4, E5).
- **Rủi ro giả định, cần quyết định chính sách:** C-RR-02/E2, C-RR-04, một phần C-RR-03, chia sẻ credential, chuyển role.

### 3.3 Trả lời 8 câu hỏi bổ sung

1. **Version của registry có được kiểm tra đủ khi commit không?**
   - Không. Không có `ConditionCheck` nào trên registry (C-RR-02).
2. **IAM có đủ chặt khi xét cùng Lambda và API Gateway không?**
   - Từng template (operator, approver, runtime) đều chặt khi xét riêng.
   - Xét cả hệ thống thì chưa: gọi Lambda cùng account, quản trị API/Lambda, người ghi registry và quản trị bảng đều chưa có control (C-RR-01, C-RR-03).
   - Chưa có gì được kiểm chứng trên AWS.
3. **Kẻ không có quyền có giả mạo được trường nào được coi là danh tính tin cậy không?**
   - Qua API Gateway với IAM auth: **không**. Các trường do AWS điền; body chứa trường danh tính bị từ chối; session name bị bỏ qua.
   - Qua gọi Lambda trực tiếp hoặc sửa integration: **có, giả được mọi trường** (E3).
   - Người ghi registry có thể bind lại role sang người khác.
4. **Approval Handler và Transition Handler đã tách đủ chưa?**
   - Về logic: có.
   - Về vật lý: không (cùng tiến trình, cùng role). Xem §3.1.
5. **Bộ ký SigV4 tự viết có đúng chuẩn API Gateway không?**
   - Đúng spec với các dạng request đang dùng:
     - Canonical request có dòng trống sau phần headers.
     - Headers được sắp xếp; khóa được dẫn xuất qua `AWS4`→ngày→region→service→`aws4_request`.
     - Path được encode hai lần (đúng cho dịch vụ không phải S3; route hiện chỉ có ký tự ASCII).
     - Có hash của payload.
     - Có `x-amz-security-token`.
     - Endpoint được thêm dấu `/` ở cuối nên stage không bị mất.
   - Vector `get-vanilla` pass.
   - **Chưa kiểm chứng:**
     - Nhánh POST có token, ngoài việc tự so với chính nó.
     - API Gateway thật chấp nhận chữ ký.
     - Khác biệt REST API và HTTP API.
     - Custom domain và base path.
     - Lệch đồng hồ quá 5 phút.
     - Region ký (đang giả định trùng `DynamoDb:Region`).
     - Khớp ARN `execute-api` theo stage/method/path.
6. **Replay, timeout hay recovery đồng thời có áp dụng lại thay đổi đặc quyền không?**
   - Không tìm thấy đường nào, qua phân tích, test và E5.
   - Phân loại sai (C-RR-05) chỉ làm báo sai kết quả; hệ thống vẫn fail-closed.
7. **Audit có bền, đúng người và chống sửa trái phép không?**
   - Bền và đúng người với các thay đổi đã áp dụng.
   - Không chống được sửa từ role của service hoặc từ quản trị bảng.
   - Sự kiện bị từ chối chỉ nằm trong log.
   - Danh tính trong SQLite chỉ là giá trị do service báo về.
   - Kết luận: **PARTIAL** (C-RR-07).
8. **Còn cần gì trước C-EX2?** Xem §10.

## 4. Đánh giá lại C-IR-01 đến C-IR-11

| ID | Lỗi gốc | Khắc phục đã làm | Bằng chứng | Còn tồn tại | Trạng thái |
|---|---|---|---|---|---|
| C-IR-01 (P1) | Credential operator `UpdateItem` tạo được trạng thái authority bất kỳ | Authority Service phía server; xóa đường ghi của operator; template Deny; server tính `state_version`/admin; tombstone | `S01CAuthorityServiceTests` (22), `S01CIamPolicyTests`, E1–E3 | C-RR-01 (context giả), C-RR-02, C-RR-03; chưa có adapter; AWS NOT VERIFIED | **PARTIAL.** Vector gốc đã khắc phục ở mức code. |
| C-IR-02 (P2) | Profile MFA không chạy được | `OperatorCredentialProvider`: chỉ chấp nhận assume-role có `mfa_serial`, có callback nhập mã không echo, session 900 giây; từ chối SSO, `credential_process`, static key | `OperatorCredentials_*` | STS thật chưa chạy. MFA chỉ thật sự được cưỡng chế bởi trust policy. Tổ chức dùng Identity Center thì cần thiết kế lại. | **REMEDIATED (code); NOT VERIFIED (STS)** |
| C-IR-03 (P2) | Bỏ qua elevation bằng biến môi trường | `IsPrivilegedOperator` ở mọi môi trường; chỉ test hoặc harness (LocalSeal) mới thay được | `Cli_Mutation_RequiresElevated*` (3) | `IsPrivilegedProcess` nghĩa là "bất kỳ admin cục bộ nào"; test chỉ phủ nhánh không nâng quyền | **REMEDIATED (code)** |
| C-IR-04 (P2) | Audit không có danh tính operator | Thêm metadata `operator_person_id`, `approver_person_id`, `identity_assurance`; không có danh tính thì không ghi SQLite; ledger và audit của service mang `person_id` + ARN | `BootstrapAndRecoveryAudit_*`, `MissingVerifiedOperatorIdentity*`, `AuditEvents_*` | Danh tính trong SQLite chỉ do service báo về (C-RR-07); log AWS chưa kiểm chứng | **REMEDIATED (code)**, có giới hạn |
| C-IR-05 (P2) | Test double và tên test nói quá | Fake DynamoDB có upsert, nhiều item và transaction; đổi tên test | `UnconditionedUpdateItem_*` | Cùng loại lỗi tái xuất ở R3: IAM simulator, test Lambda, SigV4 vòng tròn (C-RR-06, C-RR-01) | **PARTIAL** |
| C-IR-06 (P3) | Benchmark flaky | Tách collection, đo xen kẽ | Pass 2/2 lần full-suite (§8) | Mẫu nhỏ, không phải CI | **REMEDIATED (test)** |
| C-IR-07 (P3) | In sẵn email đích | Chỉ in id; operator nhập email từ yêu cầu đã duyệt | `RecoveryTargetConfirmation_*` | Host admin vẫn đọc được email trong SQLite; thủ tục C-R05 vẫn OPEN | **REMEDIATED (code)** |
| C-IR-08 (P3) | Harness mặc định `Testing` | Bắt buộc `DOTNET_ENVIRONMENT` tường minh, ASP.NET (nếu có) phải trùng, chỉ LocalSeal | `TestHarness_Refuses*` (4) | Binary harness vẫn được build khi build cả solution; LocalSeal không phải ranh giới bảo mật | **REMEDIATED (code)** |
| C-IR-09 (P3) | Chỉ kiểm tra tên trigger | So nội dung SQL đã chuẩn hóa; từ chối trigger lạ trên bảng platform | `Preflight_DetectsTampered*` (3) | Chưa phát hiện trigger gắn trên bảng **khác** mà ghi vào bảng platform | **REMEDIATED (code)** |
| C-IR-10 (P3) | Không có test kill process thật | Kill process thật sau commit SQLite (hook của harness) | `S01CProcessTerminationTests` (2) | Chỉ với LocalSeal; nhánh service/DynamoDB chưa có kill thật | **REMEDIATED (test)**, có giới hạn |
| C-IR-11 (P3) | Governance | Báo cáo remediation nêu rõ các văn bản còn thiếu | — | Văn bản nguồn chưa công bố; `PROJECT_STATUS.md` lỗi thời; tính độc lập bị giới hạn | **OPEN.** Không tự động coi là đã giải quyết. |

## 5. Đánh giá CF-01 đến CF-05

Văn bản gốc đã duyệt không có trong repo. Đánh giá dưới đây dựa trên bản tóm tắt trong báo cáo remediation và ý nghĩa nêu trong prompt (evidence gap).

| CF | Đánh giá | Lý do |
|---|---|---|
| CF-01 Danh tính | **PARTIAL** | Đúng với đường đi qua gateway đã xác thực. Còn context giả (C-RR-01), TOCTOU registry (C-RR-02), governance registry và trust policy (C-RR-03), thiếu `SourceIdentity`. AWS: NOT VERIFIED. |
| CF-02 Approval | **Tuân thủ ở mức code**, còn điểm chờ PO | Ràng buộc đầy đủ, single-use, có hạn, chặn tự phê duyệt và dùng chéo deployment, có tombstone. Chờ PO: approval của approver đã bị thu hồi (E2), hoàn tất không có hạn, không có đường hủy (C-RR-04). |
| CF-03 Transition | **PARTIAL (nhỏ)** | Tách handler, có ledger, ghi có điều kiện, không giả định SQLite và DynamoDB chung transaction. Lệch: phân loại kết quả không rõ (C-RR-05). |
| CF-04 Tương thích | **PASS (code)** | Runtime vẫn chỉ đọc, định dạng bản ghi không đổi, S01-B không đổi, có `Api_..._ContainsNoAuthorityMutationCode`, regression 414/414. |
| CF-05 Phạm vi | **PASS** | Không có migration hay thay đổi schema, không có S01-D/E/F. Thay đổi ngoài S01-C: test benchmark và 3 khóa metadata trong allowlist audit (module S01-B). PO nên xác nhận thay đổi allowlist này. |

## 6. Ma trận REM-G01 đến REM-G10

Gate được chấm theo ý nghĩa nêu trong prompt. Mock IAM **không** được coi là bằng chứng IAM production.

| Gate | Kết quả | Bằng chứng | Thiếu hoặc lỗi |
|---|---|---|---|
| REM-G01 Tách Operator/Authority | **PASS (code)** · AWS NOT VERIFIED | §3.1 dòng 1–4; `S01CAuthorityServiceTests`; template | Ranh giới thực thi trong AWS phụ thuộc G03 và C-RR-03 |
| REM-G02 Danh tính và phê duyệt độc lập | **PARTIAL** | Chặn tự phê duyệt ở 2 lớp; ánh xạ unique id | C-RR-02 đã chứng minh (E1). Nếu PO coi thu hồi tức thời là yêu cầu bắt buộc của gate thì **FAIL**. Chính sách E2. |
| REM-G03 Ranh giới invocation được bảo vệ | **FAIL** (thiết kế/bằng chứng) · AWS NOT VERIFIED | E3; ngữ nghĩa AWS cùng account; test nói quá | C-RR-01; chưa có adapter; chưa có SCP/boundary |
| REM-G04 State machine và an toàn replay | **PARTIAL** | Không có áp dụng lại; ledger và điều kiện đúng | C-RR-05 (E4, E5); ngõ cụt C-RR-04; DynamoDB thật NOT VERIFIED |
| REM-G05 Tương thích SQLite | **PASS** | Không có migration; `BEGIN IMMEDIATE`; idempotency recovery; kiểm tra trigger; 414/414 | — |
| REM-G06 Bằng chứng audit tin cậy | **PARTIAL** | Ledger và approval ghi cùng transaction, mang `person_id` | C-RR-07; log chưa có quyền ghi/template; chịu ảnh hưởng của C-RR-01 |
| REM-G07 An toàn cấu hình và credential | **PASS (code)** · STS NOT VERIFIED | Endpoint HTTPS được validate; không có credential trong config; bắt buộc MFA; elevation ở mọi môi trường; harness bị giới hạn; không có công tắc bypass | Giả định region ký; trust policy (C-RR-03) |
| REM-G08 Test bảo mật và regression | **PARTIAL** | 414/414 × 2; nhiều test âm có giá trị | C-RR-06; không phải CI |
| REM-G09 Governance, truy vết, review độc lập | **PARTIAL** | Có tài liệu truy vết R1–R6 → C-IR | C-RR-08: văn bản nguồn, phê duyệt quyết định, tính độc lập |
| REM-G10 Phạm vi và ranh giới production | **PASS** | Không có tài nguyên AWS; không deploy; adapter được nêu rõ là chưa có; `IsPackable=false`; C-G09 không bị chạm tới | — |

## 7. Đánh giá lại C-G01 đến C-G09

| Gate | Review trước | Re-review |
|---|---|---|
| C-G01 | PASS | **PASS** |
| C-G02 | PASS ở mức code | **PASS (code).** Template runtime chỉ đọc. IAM thật: NOT VERIFIED. |
| C-G03 | PASS | **PASS.** `BEGIN IMMEDIATE` không đổi; có thêm test reservation giả. |
| C-G04 | PASS | **PASS (code/fake).** Race ở server và recovery đồng thời. DynamoDB thật: NOT VERIFIED. |
| C-G05 | PASS ở mức code/test double | **PASS (code/fake).** Có kill thật (LocalSeal). P3: C-RR-05. |
| C-G06 | CÓ ĐIỀU KIỆN (C-IR-01) | **CÓ ĐIỀU KIỆN, đã cải thiện.** Operator đơn lẻ không mở lại được bootstrap (tombstone + approval). Điều kiện mới: C-RR-01, C-RR-03. Restore trong cùng epoch thuộc C-G09. |
| C-G07 | MỘT PHẦN | **MỘT PHẦN.** Đã có ủy quyền độc lập và danh tính ở mức code. Còn C-RR-02, C-R05, công cụ cho approver, AWS. |
| C-G08 | MỘT PHẦN | **MỘT PHẦN.** 414/414 × 2 lần cục bộ. Không phải CI, không phải AWS. |
| **C-G09** | OPEN | **OPEN, chặn production. Không đóng.** |

## 8. Kết quả test độc lập

### 8.1 Lệnh và kết quả

**Môi trường:** .NET SDK 10.0.101, target net8.0, Windows 11 Pro, máy cục bộ (không phải CI).

| Lệnh | Kết quả |
|---|---|
| `dotnet build src/backend/InternalKnowledgeCopilot.sln --no-incremental` | **Build succeeded, 0 Warning(s), 0 Error(s)** (16 giây) |
| `dotnet test ... --no-build` ngay sau build (lần 1) | **414 passed / 0 failed / 0 skipped** (1 phút 29 giây) |
| `dotnet test src/backend/InternalKnowledgeCopilot.sln` (lần 2, đúng lệnh được yêu cầu) | **414 / 0 / 0** (1 phút 22 giây) |

- TRX của cả 2 lần đều có 149 test S01-C và 0 test fail. `PasswordHasher_Benchmark` pass cả 2 lần.
- **Kết luận:** claim 414/414 và 149 test S01-C **tái tạo được**.

### 8.2 Chất lượng test

- **Điểm mạnh:**
  - Dùng SQLite thật và tiến trình OS thật.
  - Có test âm ở server: tự phê duyệt qua role hoặc session khác, dùng chéo deployment, version cũ, race 8 request, replay bằng ledger, kết quả không rõ, bóc danh tính khỏi phản hồi, tombstone.
- **Điểm yếu:** xem C-RR-06 và C-RR-01.

### 8.3 Thí nghiệm bổ sung (ngoài repo)

- Thí nghiệm chạy trên `InternalKnowledgeCopilot.AuthorityService.dll` build từ HEAD `10be150`.
- Dùng một store trong bộ nhớ có ngữ nghĩa điều kiện giống `DynamoDbAuthorityStore`: `attribute_not_exists` hoặc `version = expected`, tất cả hoặc không gì cả.

| # | Thí nghiệm | Kết quả |
|---|---|---|
| E1 | Tắt operator trong registry sau khi resolve, trước khi commit | `reserve` **Applied**, bản ghi `Reserved`. Request tiếp theo bị `principal_disabled`. |
| E2 | Tắt approver sau khi đã cấp approval | Operator vẫn `reserve` **Applied**. |
| E3 | Context gateway giả nhưng đúng định dạng, không có SigV4 | **Applied** `approval_issued` dưới tên "bob". |
| E4 | `TransactionInProgressException` (HTTP 400) khi commit | `OutcomeUnknown=False`; endpoint trả `503 Rejected` |
| E5 | Cùng `request_id` gửi chồng nhau | Một lần `Applied`, lần kia `409 Rejected authority_concurrent_modification` |

## 9. Các khoảng trống bằng chứng phụ thuộc AWS (NOT VERIFIED)

1. **Adapter Lambda chưa có.** Chưa chốt REST hay HTTP API; chưa xử lý `test-invoke-stage`; chưa validate chặt schema của event.
2. **IAM thật:** template chưa được nạp; SCP và permission boundary; `LeadingKeys` khi dùng `TransactWriteItems`; cách đánh giá `execute-api:Invoke`; hành vi invoke Lambda cùng account; câu lệnh Deny trong function policy.
3. **API Gateway:** IAM auth, các trường `requestContext` thật, resource policy, custom domain, kiểm tra SigV4 thật (lệch đồng hồ, session token).
4. **STS/MFA:** `AssumeRole` có MFA thật, `aws:MultiFactorAuthAge`, `SourceIdentity`, cách phát hành danh tính theo người.
5. **DynamoDB:** ngữ nghĩa thật của `TransactWriteItems`/`CancellationReasons`/`ClientRequestToken`/`TransactionInProgress`; kích thước item registry; PITR; deletion protection; KMS; resource policy của bảng.
6. **Log và giám sát:** CloudWatch (quyền, retention, KMS, chống xóa); CloudTrail data events; cảnh báo khi có ghi ngoài service (DynamoDB Streams).
7. **Quy trình Security Operations** cho registry: tạo, thu hồi, không chuyển role, kiểm soát hai người.
8. **Restore drill** (C-G09).

## 10. Khuyến nghị khắc phục và các gate cần PO phê duyệt

| ID | Khắc phục | Loại | Cần PO? | Trước gate nào |
|---|---|---|---|---|
| C-RR-01 | (a) Tài khoản authority riêng + SCP/boundary chặn invoke và quản trị Lambda/API. (b) Bằng chứng danh tính đầu-cuối (`GetCallerIdentity` ký sẵn). (c) Adapter với schema chặt. (d) Sửa test và tài liệu R3. | Thiết kế + IAM + code | **Có:** chọn phương án | Freeze thiết kế C-EX2 |
| C-RR-02 | `ConditionCheck` version registry trong mọi commit; quyết định có kiểm tra lại approver ở bước claim hay không; thêm test | Code + chính sách | Có (vòng remediation nhỏ) | C-EX2 |
| C-RR-03 | Resource policy cho bảng; template và quy trình ghi registry (hai người); Deny theo ARN dùng `NotAction`; quyền log; trust policy ràng buộc IAM user cụ thể + `SourceIdentity`; resource policy cho API; sửa comment | IAM + quy trình | Có | C-EX2 |
| C-RR-04 | Thời gian chờ hoặc xác nhận trước `ReauthorizeBootstrap`; action reconcile reservation đã bị thay thế; hạn giữ approval và đường hủy hai người; break-glass cho `Revoked` | Chính sách (+ code) | **Có:** quyết định chính sách | Trước production; nên chốt trước C-EX2 |
| C-RR-05 | Phân loại lại lỗi "không rõ"; đọc lại ledger khi xung đột ledger; mô phỏng trong fake | Code + test | Có (vòng nhỏ) | C-EX2 |
| C-RR-06 | Đổi tên hoặc sửa khẳng định; thêm vector SigV4 `post-sts-token` hoặc so sánh với signer của AWSSDK; thêm các test âm đã nêu | Test | Có (vòng nhỏ) | C-EX2 |
| C-RR-07 | Lưu trữ append-only cho ledger/approval; ghi rõ mức đảm bảo của danh tính SQLite trong AUD-01 | IAM + tài liệu | Có | Trước production |
| C-RR-08 | Công bố ADR-01, Design v1.0, Plan v1.0, CF-01–05, REM-G01–10; phê duyệt các quyết định implementer tự chọn; cập nhật `PROJECT_STATUS.md`; reviewer bảo mật là con người | Governance | Việc của PO | Ký REM-G09 |

**Các gate PO cần phê duyệt:**

1. **PO-A:** chọn phương án cho C-RR-01, trước khi freeze thiết kế C-EX2.
2. **PO-B:** ủy quyền một vòng remediation nhỏ (gợi ý tên REM-EX2) cho C-RR-02, C-RR-05, C-RR-06, phần template của C-RR-03 và việc sửa tài liệu.
3. **PO-C:** quyết định chính sách cho:
   - Ngữ nghĩa thu hồi (E2).
   - Hạn giữ approval và đường hủy.
   - Điều kiện cho `ReauthorizeBootstrap`.
   - Break-glass cho `Revoked`.
   - Hoàn tất không có hạn.
   - Không chuyển giao approval.
   - REST hay HTTP API.
   - Tài khoản authority riêng.
4. **PO-D:** công bố văn bản nguồn và cập nhật `PROJECT_STATUS.md`.
5. **PO-E:** sign-off của một reviewer bảo mật độc lập về tổ chức.
6. **PO-F:** ủy quyền C-EX2. Đây là quyết định riêng; báo cáo này không ủy quyền.
7. **C-G09:** vẫn OPEN, là một quyết định riêng và chặn production.

**Trước C-EX2 còn cần:**
- **Thành phần:**
  - Adapter Lambda.
  - Công cụ cho approver.
  - Công cụ và quy trình quản lý registry.
  - Resource policy cho bảng và cho API.
  - SCP hoặc boundary.
  - Log group và quyền log.
  - CloudTrail data events và cảnh báo.
  - Tùy chọn: bằng chứng danh tính đầu-cuối.
- **Thủ tục:**
  - C-R05 (điều tra trước khi reauthorize; kênh gửi email đích).
  - Vòng đời registry và thiết bị MFA.
  - Break-glass.
  - Xử lý trạng thái ngõ cụt.
  - Restore (C-G09).
- **Phê duyệt:** PO-A đến PO-F.

## 11. Kết luận cuối

**REMEDIATION REQUIRED**

- Hướng ADR-01 đúng. Vector gốc của C-IR-01 đã bị loại bỏ ở mức code. Phần SQLite/CLI và state machine phía server đạt chất lượng tốt. Không có P0. Không tìm thấy đường áp dụng lại thay đổi đặc quyền.
- Chưa thể **ACCEPT FOR NEXT GATE** vì:
  - REM-G03 FAIL (C-RR-01, P1): ranh giới invocation chưa được thiết lập, trong khi bằng chứng lại khẳng định là đã có.
  - Có một defect P2 đã được chứng minh (C-RR-02).
  - Template IAM, vốn là đầu vào của C-EX2, còn thiếu (C-RR-03).
  - Nếu đưa nguyên trạng vào C-EX2, các khẳng định sai sẽ đi theo vào bước provisioning.
- **Không BLOCKED.** Các mục còn lại có phạm vi rõ và không đòi đổi kiến trúc. Nếu PO muốn chạy song song, có thể cho phép C-EX2 **ở mức thiết kế** (không provisioning), với điều kiện C-RR-01 là đầu vào bắt buộc.
- **Không authorize merge PR #8/#9, provisioning AWS hay deploy production. C-G09 vẫn OPEN và chặn production.**
