using System.Net.Http.Headers;
using System.Text.Json;
using Amazon.Runtime;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;

namespace InternalKnowledgeCopilot.PlatformCli.Authority;

// Đường production của operator (DynamoDb): gọi các route operation của Authority Service qua API Gateway, ký SigV4 bằng
// credential assume-role có MFA của chính operator. Operator không có quyền ghi DynamoDB nào; danh tính trong phản hồi là
// person_id do Authority Service xác minh.
public sealed class AuthorityServiceHttpClient(HttpClient http, Uri endpoint, AWSCredentials credentials, string region, TimeProvider clock)
    : IAuthorityTransitionClient
{
    public const string SigningService = "execute-api";

    public Task<AuthorityStepResult> ReserveAsync(ProvisioningAuthorityRecord authorized, Guid reservedAdminId, string requestId, CancellationToken cancellationToken = default)
    {
        // Bản ghi Authorized do Authority Service cấp có authorization_id là operation id (GUID).
        if (!Guid.TryParseExact(authorized.AuthorizationId, "D", out var operationId))
        {
            return Task.FromResult(AuthorityStepResult.Rejected("authority_record_not_service_issued"));
        }

        return PostAsync(AuthorityServiceRoutes.Reserve, new AuthorityOperationRequest
        {
            RequestId = requestId,
            DeploymentId = authorized.DeploymentId,
            OperationId = operationId,
            ExpectedAuthorityVersion = authorized.Version,
            ReservedAdminId = reservedAdminId,
        }, cancellationToken);
    }

    public Task<AuthorityStepResult> ActivateAsync(ProvisioningAuthorityRecord reserved, DateTimeOffset bootstrapCompletedAt, string requestId, CancellationToken cancellationToken = default)
    {
        return PostAsync(AuthorityServiceRoutes.Activate, new AuthorityOperationRequest
        {
            RequestId = requestId,
            DeploymentId = reserved.DeploymentId,
            OperationId = reserved.ReservationId!.Value,
            ExpectedAuthorityVersion = reserved.Version,
            BootstrapCompletedAt = bootstrapCompletedAt,
        }, cancellationToken);
    }

    public Task<AuthorityStepResult> ClaimRecoveryAsync(ProvisioningAuthorityRecord pending, string requestId, CancellationToken cancellationToken = default)
    {
        return PostAsync(AuthorityServiceRoutes.ClaimRecovery, new AuthorityOperationRequest
        {
            RequestId = requestId,
            DeploymentId = pending.DeploymentId,
            OperationId = pending.RecoveryId!.Value,
            ExpectedAuthorityVersion = pending.Version,
        }, cancellationToken);
    }

    public Task<AuthorityStepResult> CompleteRecoveryAsync(
        ProvisioningAuthorityRecord pending,
        int stateVersion,
        DateTimeOffset? bootstrapCompletedAt,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        return PostAsync(AuthorityServiceRoutes.CompleteRecovery, new AuthorityOperationRequest
        {
            RequestId = requestId,
            DeploymentId = pending.DeploymentId,
            OperationId = pending.RecoveryId!.Value,
            ExpectedAuthorityVersion = pending.Version,
            StateVersion = stateVersion,
            BootstrapCompletedAt = bootstrapCompletedAt,
        }, cancellationToken);
    }

    private async Task<AuthorityStepResult> PostAsync(string route, AuthorityOperationRequest body, CancellationToken cancellationToken)
    {
        ImmutableCredentials signing;
        try
        {
            // Với profile assume-role có MFA, bước này hỏi mã MFA (không echo) rồi gọi STS.
            signing = await credentials.GetCredentialsAsync();
        }
        catch (Exception ex) when (ex is AmazonClientException or AmazonServiceException or InvalidOperationException)
        {
            return AuthorityStepResult.Rejected("operator_credentials_unavailable");
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(body, AuthorityServiceJson.Options);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, AuthorityServiceRoutes.PathOf(route).TrimStart('/')))
        {
            Content = new ByteArrayContent(payload),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        SigV4Signer.Sign(request, payload, signing, region, SigningService, clock.GetUtcNow());

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthorityStepResult.Unknown("authority_service_timeout");
        }
        catch (HttpRequestException)
        {
            return AuthorityStepResult.Unknown("authority_service_unreachable");
        }

        using (response)
        {
            AuthorityServiceResponse? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<AuthorityServiceResponse>(await response.Content.ReadAsStringAsync(cancellationToken), AuthorityServiceJson.Options);
            }
            catch (JsonException)
            {
            }

            if (parsed is null)
            {
                // Lỗi gateway/5xx không có thân hợp lệ: không biết đã áp dụng chưa. 4xx: gateway từ chối trước khi tới service.
                return (int)response.StatusCode >= 500
                    ? AuthorityStepResult.Unknown("authority_service_error")
                    : AuthorityStepResult.Rejected("authority_service_rejected");
            }

            // Mã lý do từ service được in ra console: chỉ chấp nhận định danh an toàn.
            if (!ProvisioningAuthorityRecord.IsSafeIdentifier(parsed.Reason))
            {
                parsed = parsed with { Reason = "authority_service_reason_invalid" };
            }

            return parsed.Outcome switch
            {
                AuthorityResponseOutcome.Applied or AuthorityResponseOutcome.Replayed when parsed.CallerPersonId is { } person
                    && ProvisioningAuthorityRecord.IsSafeIdentifier(person)
                    => AuthorityStepResult.Applied(parsed.Reason, new OperatorIdentity(
                        person,
                        OperatorIdentity.ServiceVerified,
                        ProvisioningAuthorityRecord.IsSafeIdentifier(parsed.ApproverPersonId) ? parsed.ApproverPersonId : null)),
                AuthorityResponseOutcome.Applied or AuthorityResponseOutcome.Replayed => AuthorityStepResult.Applied(parsed.Reason, null),
                AuthorityResponseOutcome.Rejected => AuthorityStepResult.Rejected(parsed.Reason),
                _ => AuthorityStepResult.Unknown(parsed.Reason),
            };
        }
    }
}
