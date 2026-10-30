using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace InternalKnowledgeCopilot.PlatformCli.Authority;

// C-IR-02: credential của operator luôn là assume-role vào role riêng của người đó, có MFA. CLI tự dựng AssumeRoleAWSCredentials
// với MfaTokenCodeCallback (nhập mã không echo) thay vì để SDK tạo credential thiếu callback. Không chấp nhận access key dài hạn
// trực tiếp, SSO (cần dependency mới, chưa được duyệt), credential_process hay credential_source.
public static class OperatorCredentialProvider
{
    public const int SessionDurationSeconds = 900;

    public static AWSCredentials Create(string profileName, IOperatorConsole console, string? profilesLocation = null)
    {
        var chain = profilesLocation is null ? new CredentialProfileStoreChain() : new CredentialProfileStoreChain(profilesLocation);
        if (!chain.TryGetProfile(profileName, out var profile))
        {
            throw new CliConfigurationException("operator_profile_not_found");
        }

        var options = profile.Options;
        if (!string.IsNullOrEmpty(options.SsoStartUrl) || !string.IsNullOrEmpty(options.SsoSession) || !string.IsNullOrEmpty(options.SsoAccountId))
        {
            throw new CliConfigurationException("operator_profile_sso_not_supported");
        }

        if (!string.IsNullOrEmpty(options.CredentialProcess) || !string.IsNullOrEmpty(options.CredentialSource) || !string.IsNullOrEmpty(options.WebIdentityTokenFile))
        {
            throw new CliConfigurationException("operator_profile_source_not_supported");
        }

        if (string.IsNullOrEmpty(options.RoleArn) || string.IsNullOrEmpty(options.SourceProfile))
        {
            throw new CliConfigurationException("operator_profile_must_assume_role");
        }

        if (string.IsNullOrEmpty(options.MfaSerial))
        {
            throw new CliConfigurationException("operator_profile_mfa_required");
        }

        if (!chain.TryGetAWSCredentials(options.SourceProfile, out var sourceCredentials))
        {
            throw new CliConfigurationException("operator_source_profile_not_found");
        }

        // Session name chỉ để truy vết trong CloudTrail; Authority Service không bao giờ dùng nó làm danh tính.
        var sessionName = string.IsNullOrEmpty(options.RoleSessionName) ? $"ikc-platform-cli-{Guid.NewGuid():N}"[..40] : options.RoleSessionName;
        return new AssumeRoleAWSCredentials(sourceCredentials, options.RoleArn, sessionName, new AssumeRoleAWSCredentialsOptions
        {
            MfaSerialNumber = options.MfaSerial,
            MfaTokenCodeCallback = () => console.ReadSecret("MFA code: ") is { Length: > 0 } code
                ? code.Trim()
                : throw new InvalidOperationException("mfa_code_required"),
            ExternalId = string.IsNullOrEmpty(options.ExternalID) ? null : options.ExternalID,
            DurationSeconds = SessionDurationSeconds,
        });
    }
}
