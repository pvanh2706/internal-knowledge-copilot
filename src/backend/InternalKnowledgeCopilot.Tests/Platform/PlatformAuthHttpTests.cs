using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Authentication;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

namespace InternalKnowledgeCopilot.Tests.Platform;

// B3/B5: đăng nhập local Platform Admin, khóa tài khoản, phiên giới hạn, thu hồi token và audit qua pipeline thật.
public sealed class PlatformAuthHttpTests(S01BApiFactory factory) : IClassFixture<S01BApiFactory>
{
    private const string Me = "/api/platform/auth/me";
    private const string ChangePassword = "/api/platform/auth/change-password";
    private const string AdminProbe = "/api/platform/test-probe/platform-admin";

    [Fact]
    public async Task Login_WithValidCredentials_IssuesShortLivedPlatformTokenAndAudits()
    {
        var admin = await factory.SeedPlatformAdminAsync("valid@example.com");

        using var response = await factory.PlatformLoginAsync("  Valid@Example.com ", S01BApiFactory.PlatformPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PlatformLoginResponse>())!;
        Assert.False(body.MustChangePassword);
        Assert.Equal(PlatformSessionTypes.Full, body.SessionType);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Equal(admin.Id.ToString(), jwt.Subject);
        Assert.Equal(PrincipalTypes.Platform, jwt.Claims.Single(claim => claim.Type == PrincipalClaimTypes.PrincipalType).Value);
        Assert.Equal("0", jwt.Claims.Single(claim => claim.Type == PrincipalClaimTypes.TokenVersion).Value);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type is "tenant_id" or "tenant_code" or "role");
        Assert.InRange(jwt.ValidTo - jwt.ValidFrom, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));

        using var me = await factory.SendAsync(HttpMethod.Get, Me, body.AccessToken);
        using var probe = await factory.SendAsync(HttpMethod.Get, AdminProbe, body.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        Assert.NotNull((await factory.GetPlatformAdminAsync(admin.Id)).LastLoginAt);

        var audit = (await factory.GetPlatformAuditAsync()).Last(item => item.ActorPlatformId == admin.Id);
        Assert.Equal(PlatformAuditActions.LoginSucceeded, audit.Action);
        Assert.Equal(PlatformAuditOutcome.Success, audit.Outcome);
        Assert.Null(audit.TargetTenantId);
        Assert.False(string.IsNullOrWhiteSpace(audit.RequestId));
    }

    [Fact]
    public async Task Login_UnknownAccountWrongPasswordDisabledAndLocked_AreIndistinguishable()
    {
        await factory.SeedPlatformAdminAsync("known@example.com");
        await factory.SeedPlatformAdminAsync("disabled@example.com", status: PlatformAdminStatus.Disabled);
        await factory.SeedPlatformAdminAsync("locked@example.com", status: PlatformAdminStatus.Locked);

        var responses = new List<(HttpStatusCode Status, string Body)>();
        foreach (var (email, password) in new[]
        {
            ("unknown@example.com", S01BApiFactory.PlatformPassword),
            ("known@example.com", "Wrong-Password-123"),
            ("disabled@example.com", S01BApiFactory.PlatformPassword),
            ("locked@example.com", S01BApiFactory.PlatformPassword),
            ("unicode-é@example.com", S01BApiFactory.PlatformPassword),
            ("admin@example.local", S01BApiFactory.SeededTenantPassword),
        })
        {
            using var response = await factory.PlatformLoginAsync(email, password);
            responses.Add((response.StatusCode, await response.Content.ReadAsStringAsync()));
        }

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.Status));
        Assert.Single(responses.Select(response => response.Body).Distinct());
        Assert.DoesNotContain("disabled", responses[0].Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_RepeatedFailures_LockAccountOnceAndCorrectPasswordIsThenRefused()
    {
        var admin = await factory.SeedPlatformAdminAsync("lockout@example.com");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failure = await factory.PlatformLoginAsync("lockout@example.com", "Wrong-Password-123");
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        using var afterLock = await factory.PlatformLoginAsync("lockout@example.com", S01BApiFactory.PlatformPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, afterLock.StatusCode);
        var stored = await factory.GetPlatformAdminAsync(admin.Id);
        Assert.Equal(PlatformAdminStatus.Locked, stored.Status);
        Assert.Equal(5, stored.FailedLoginCount);
        Assert.NotNull(stored.LockedUntil);
        var audit = (await factory.GetPlatformAuditAsync()).Where(item => item.ActorPlatformId == admin.Id).ToList();
        Assert.Equal(5, audit.Count(item => item.Action == PlatformAuditActions.LoginFailed));
        Assert.Single(audit, item => item.Action == PlatformAuditActions.Lockout);
        Assert.Equal(PlatformAuditActions.LoginDenied, audit.Last().Action);
        Assert.Contains("account_locked", audit.Last().MetadataJson);
    }

    [Fact]
    public async Task Login_AfterLockExpires_SucceedsAndResetsCounter()
    {
        var admin = await factory.SeedPlatformAdminAsync("expired-lock@example.com");
        await factory.ExecuteSqlAsync(
            $"UPDATE platform_admins SET status = 'Locked', failed_login_count = 5, locked_until = '2020-01-01 00:00:00+00:00' WHERE id = '{admin.Id.ToString().ToUpperInvariant()}'");

        using var response = await factory.PlatformLoginAsync("expired-lock@example.com", S01BApiFactory.PlatformPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await factory.GetPlatformAdminAsync(admin.Id);
        Assert.Equal(PlatformAdminStatus.Active, stored.Status);
        Assert.Equal(0, stored.FailedLoginCount);
        Assert.Null(stored.LockedUntil);
    }

    [Fact]
    public async Task Login_ConcurrentFailures_AreCountedWithoutLostUpdatesAndLockOnce()
    {
        var admin = await factory.SeedPlatformAdminAsync("concurrent@example.com");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => factory.PlatformLoginAsync("concurrent@example.com", "Wrong-Password-123")));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        foreach (var response in responses)
        {
            response.Dispose();
        }

        var stored = await factory.GetPlatformAdminAsync(admin.Id);
        var audit = (await factory.GetPlatformAuditAsync()).Where(item => item.ActorPlatformId == admin.Id).ToList();
        Assert.Equal(PlatformAdminStatus.Locked, stored.Status);
        Assert.Equal(5, stored.FailedLoginCount);
        Assert.Equal(5, audit.Count(item => item.Action == PlatformAuditActions.LoginFailed));
        Assert.Single(audit, item => item.Action == PlatformAuditActions.Lockout);
        Assert.Equal(3, audit.Count(item => item.Action == PlatformAuditActions.LoginDenied));
    }

    [Theory]
    [InlineData("$argon2id$v=19$m=1,t=1,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$argon2id$v=19$m=999999999,t=99,p=99$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$argon2i$v=19$m=65536,t=3,p=1$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("$pbkdf2-sha256$v=1$i=100000$c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$not*base64$x")]
    public async Task Login_WithMalformedOrUnsupportedStoredHash_FailsSafely(string storedHash)
    {
        var email = $"malformed-{Guid.NewGuid().ToString("N")[..8]}@example.com";
        var admin = await factory.SeedPlatformAdminAsync(email);
        await factory.ExecuteSqlAsync($"UPDATE platform_admins SET password_hash = '{storedHash}' WHERE id = '{admin.Id.ToString().ToUpperInvariant()}'");

        using var response = await factory.PlatformLoginAsync(email, S01BApiFactory.PlatformPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, (await factory.GetPlatformAdminAsync(admin.Id)).FailedLoginCount);
    }

    [Fact]
    public async Task FirstLogin_RestrictedSession_CanOnlyReadIdentityAndChangePassword()
    {
        var admin = await factory.SeedPlatformAdminAsync("first-login@example.com", mustChangePassword: true);

        using var login = await factory.PlatformLoginAsync("first-login@example.com", S01BApiFactory.PlatformPassword);
        var body = (await login.Content.ReadFromJsonAsync<PlatformLoginResponse>())!;
        using var me = await factory.SendAsync(HttpMethod.Get, Me, body.AccessToken);
        using var probe = await factory.SendAsync(HttpMethod.Get, AdminProbe, body.AccessToken);

        Assert.True(body.MustChangePassword);
        Assert.Equal(PlatformSessionTypes.PasswordChangeOnly, body.SessionType);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, probe.StatusCode);
        var denial = (await factory.GetPlatformAuditAsync()).Last(item => item.ActorPlatformId == admin.Id);
        Assert.Equal(PlatformAuditActions.AccessDenied, denial.Action);
        Assert.Contains("restricted_session", denial.MetadataJson);

        // Token phiên đầy đủ bị làm giả cho tài khoản vẫn phải đổi mật khẩu bị từ chối.
        var forgedFull = S01BApiFactory.CreateToken(S01BApiFactory.PlatformSigningKey, S01BApiFactory.PlatformIssuer, S01BApiFactory.PlatformAudience,
            S01BApiFactory.PlatformClaims(admin.Id, session: PlatformSessionTypes.Full));
        using var forged = await factory.SendAsync(HttpMethod.Get, AdminProbe, forgedFull);
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);

        using var changed = await factory.SendAsync(HttpMethod.Post, ChangePassword, body.AccessToken,
            new { currentPassword = S01BApiFactory.PlatformPassword, newPassword = "Brand-New-Password-2" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        var fullToken = await factory.PlatformTokenAsync("first-login@example.com", "Brand-New-Password-2");
        using var fullProbe = await factory.SendAsync(HttpMethod.Get, AdminProbe, fullToken);
        Assert.Equal(HttpStatusCode.OK, fullProbe.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RevokesOldTokensAndOldPassword()
    {
        var admin = await factory.SeedPlatformAdminAsync("rotate@example.com");
        var oldToken = await factory.PlatformTokenAsync("rotate@example.com");

        using var changed = await factory.SendAsync(HttpMethod.Post, ChangePassword, oldToken,
            new { currentPassword = S01BApiFactory.PlatformPassword, newPassword = "Rotated-Password-22" });
        using var oldTokenMe = await factory.SendAsync(HttpMethod.Get, Me, oldToken);
        using var oldPassword = await factory.PlatformLoginAsync("rotate@example.com", S01BApiFactory.PlatformPassword);
        using var newPassword = await factory.PlatformLoginAsync("rotate@example.com", "Rotated-Password-22");

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenMe.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
        var stored = await factory.GetPlatformAdminAsync(admin.Id);
        Assert.Equal(1, stored.TokenVersion);
        Assert.False(stored.MustChangePassword);
        var audit = (await factory.GetPlatformAuditAsync()).Where(item => item.ActorPlatformId == admin.Id).Select(item => item.Action).ToList();
        Assert.Contains(PlatformAuditActions.PasswordChanged, audit);
        Assert.Contains(PlatformAuditActions.TokensRevoked, audit);
        Assert.Contains(PlatformAuditActions.SessionRejected, audit);
    }

    [Fact]
    public async Task ChangePassword_RejectsWrongCurrentWeakAndReusedPasswords()
    {
        var admin = await factory.SeedPlatformAdminAsync("policy@example.com");
        var token = await factory.PlatformTokenAsync("policy@example.com");

        using var wrongCurrent = await factory.SendAsync(HttpMethod.Post, ChangePassword, token,
            new { currentPassword = "Not-The-Password-1", newPassword = "Another-Password-33" });
        using var tooShort = await factory.SendAsync(HttpMethod.Post, ChangePassword, token,
            new { currentPassword = S01BApiFactory.PlatformPassword, newPassword = "short" });
        using var reused = await factory.SendAsync(HttpMethod.Post, ChangePassword, token,
            new { currentPassword = S01BApiFactory.PlatformPassword, newPassword = S01BApiFactory.PlatformPassword });
        using var stillValid = await factory.SendAsync(HttpMethod.Get, Me, token);

        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
        var stored = await factory.GetPlatformAdminAsync(admin.Id);
        Assert.Equal(0, stored.TokenVersion);
        Assert.Equal(1, stored.FailedLoginCount);
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("Locked")]
    public async Task ExistingToken_LosesAccessWhenAccountStatusChanges(string status)
    {
        var admin = await factory.SeedPlatformAdminAsync($"status-{status.ToLowerInvariant()}@example.com");
        var token = await factory.PlatformTokenAsync($"status-{status.ToLowerInvariant()}@example.com");
        await factory.ExecuteSqlAsync($"UPDATE platform_admins SET status = '{status}' WHERE id = '{admin.Id.ToString().ToUpperInvariant()}'");

        using var me = await factory.SendAsync(HttpMethod.Get, Me, token);
        using var probe = await factory.SendAsync(HttpMethod.Get, AdminProbe, token);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, probe.StatusCode);
        var rejection = (await factory.GetPlatformAuditAsync()).Last(item => item.ActorPlatformId == admin.Id);
        Assert.Equal(PlatformAuditActions.SessionRejected, rejection.Action);
        Assert.Contains("account_inactive", rejection.MetadataJson);
    }

    [Fact]
    public async Task AuditTrail_NeverContainsPasswordsHashesOrTokens()
    {
        var admin = await factory.SeedPlatformAdminAsync("secrets@example.com");
        using (await factory.PlatformLoginAsync("secrets@example.com", "Wrong-Secret-Password-9")) { }
        var token = await factory.PlatformTokenAsync("secrets@example.com");
        using (await factory.SendAsync(HttpMethod.Post, ChangePassword, token,
            new { currentPassword = S01BApiFactory.PlatformPassword, newPassword = "Secret-New-Password-7" })) { }
        var storedHash = (await factory.GetPlatformAdminAsync(admin.Id)).PasswordHash;

        var audit = await factory.GetPlatformAuditAsync();
        var serialized = string.Join("\n", audit.Select(item => $"{item.Action}|{item.TargetType}|{item.RequestId}|{item.MetadataJson}"));

        Assert.All(audit, item => Assert.Null(item.TargetTenantId));
        foreach (var secret in new[] { S01BApiFactory.PlatformPassword, "Wrong-Secret-Password-9", "Secret-New-Password-7", storedHash, token, "$argon2id", S01BApiFactory.PlatformSigningKey })
        {
            Assert.DoesNotContain(secret, serialized, StringComparison.Ordinal);
        }
    }
}
