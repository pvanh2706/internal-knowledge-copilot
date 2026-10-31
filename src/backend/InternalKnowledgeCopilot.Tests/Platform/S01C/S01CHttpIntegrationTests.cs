using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Platform.Provisioning;
using InternalKnowledgeCopilot.AuthorityService;
using InternalKnowledgeCopilot.PlatformCli;
using InternalKnowledgeCopilot.PlatformCli.Authority;
using InternalKnowledgeCopilot.PlatformCli.TestHarness;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InternalKnowledgeCopilot.Tests.Platform.S01C;

// S01-C qua pipeline API thật (Program.cs, PlatformJwt, PlatformPrincipalValidator, PlatformProvisioningGate) với verifier
// S01-C thật (không dùng FakeProvisioningVerifier của S01-B). CLI chạy trên cùng file SQLite.
public sealed class S01CHttpIntegrationTests
{
    private const string AuthorityId = "s01c-http-authority";
    private const string Email = "platform.owner@example.com";
    private const string InitialPassword = "Initial-Bootstrap-Pass-1";
    private const string ChangedPassword = "Changed-After-Login-Pass-2";
    private const string RecoveryPassword = "Recovery-Issued-Pass-3";
    private const string ProbePath = "/api/platform/test-probe/platform-admin";

    [Fact]
    public async Task EndToEnd_LocalSeal_BootstrapRestrictedSessionPasswordChangeRecoveryAndTokenRevocation()
    {
        using var harness = new LocalSealHarness();
        using var factory = harness.CreateFactory();

        // Trước bootstrap: platform auth không sẵn sàng và không có audit nào được ghi.
        using (var before = await factory.PlatformLoginAsync(Email, InitialPassword))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, before.StatusCode);
        }

        Assert.Empty(await factory.GetPlatformAuditAsync());

        harness.Simulator.Authorize();
        var bootstrap = await harness.RunCliAsync(factory, ["bootstrap", "--email", Email, "--display-name", "Owner"], [InitialPassword, InitialPassword, "BOOTSTRAP"]);
        Assert.True(bootstrap.ExitCode == CliExitCodes.Success, bootstrap.Output);

        // Đăng nhập lần đầu chỉ có phiên đổi mật khẩu (must_change_password=true).
        var login = await LoginAsync(factory, InitialPassword);
        Assert.True(login.MustChangePassword);
        Assert.Equal("password_change", login.SessionType);
        using (var restricted = await factory.SendAsync(HttpMethod.Get, ProbePath, login.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, restricted.StatusCode);
        }

        using (var change = await factory.SendAsync(HttpMethod.Post, "/api/platform/auth/change-password", login.AccessToken,
            new { currentPassword = InitialPassword, newPassword = ChangedPassword }))
        {
            Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        }

        var full = await LoginAsync(factory, ChangedPassword);
        Assert.Equal("full", full.SessionType);
        using (var probe = await factory.SendAsync(HttpMethod.Get, ProbePath, full.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        }

        // Authority cấp RecoveryPending: mọi platform token bị từ chối ngay lập tức (đọc lại mỗi request, không cache).
        var adminId = (await factory.WithDbAsync(db => db.PlatformAdmins.AsNoTracking().SingleAsync())).Id;
        harness.Simulator.AuthorizeAdminRecovery(adminId);
        using (var pending = await factory.SendAsync(HttpMethod.Get, ProbePath, full.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, pending.StatusCode);
        }

        using (var pendingLogin = await factory.PlatformLoginAsync(Email, ChangedPassword))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, pendingLogin.StatusCode);
        }

        var recovery = await harness.RunCliAsync(factory, ["recover-admin", "--email", Email], [RecoveryPassword, RecoveryPassword, "RECOVER"]);
        Assert.True(recovery.ExitCode == CliExitCodes.Success, recovery.Output);

        // Token cũ không hồi sinh sau recovery (token_version tăng); mật khẩu cũ hết hiệu lực; mật khẩu recovery chỉ cho phiên hạn chế.
        using (var stale = await factory.SendAsync(HttpMethod.Get, ProbePath, full.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        }

        using (var oldPassword = await factory.PlatformLoginAsync(Email, ChangedPassword))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        }

        var recovered = await LoginAsync(factory, RecoveryPassword);
        Assert.True(recovered.MustChangePassword);
        Assert.Equal("password_change", recovered.SessionType);

        var actions = (await factory.GetPlatformAuditAsync()).Select(log => log.Action).ToList();
        Assert.Contains(PlatformAuditActions.BootstrapCompleted, actions);
        Assert.Contains(PlatformAuditActions.AdminRecovered, actions);
        Assert.Contains(PlatformAuditActions.TokensRevoked, actions);
        Assert.Contains(PlatformAuditActions.SessionRejected, actions);
        Assert.DoesNotContain(factory.Logs.Messages, message => message.Contains(InitialPassword) || message.Contains(RecoveryPassword) || message.Contains(ChangedPassword));
    }

    [Fact]
    public async Task Runtime_AuthorityRevokedOrTampered_DeniesPlatformAuthentication()
    {
        using var harness = new LocalSealHarness();
        using var factory = harness.CreateFactory();
        _ = factory.Services;
        harness.Simulator.Authorize();
        Assert.Equal(CliExitCodes.Success, (await harness.RunCliAsync(factory, ["bootstrap", "--email", Email, "--display-name", "Owner"], [InitialPassword, InitialPassword, "BOOTSTRAP"])).ExitCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.PlatformLoginAsync(Email, InitialPassword)).StatusCode);

        var activatedSeal = await File.ReadAllBytesAsync(harness.SealPath);
        harness.Simulator.Revoke();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await factory.PlatformLoginAsync(Email, InitialPassword)).StatusCode);

        // Seal bị sửa (MAC không khớp) cũng fail closed.
        var tampered = (byte[])activatedSeal.Clone();
        tampered[tampered.Length / 2] ^= 0x01;
        await File.WriteAllBytesAsync(harness.SealPath, tampered);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await factory.PlatformLoginAsync(Email, InitialPassword)).StatusCode);

        File.Delete(harness.SealPath);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await factory.PlatformLoginAsync(Email, InitialPassword)).StatusCode);

        await File.WriteAllBytesAsync(harness.SealPath, activatedSeal);
        Assert.Equal(HttpStatusCode.OK, (await factory.PlatformLoginAsync(Email, InitialPassword)).StatusCode);
    }

    [Fact]
    public async Task EndToEnd_DynamoDbTestDouble_RuntimeUsesReadOnlyConsistentReads()
    {
        var deploymentId = Guid.NewGuid();
        var table = new FakeDynamoDbTable("ikc-http-authority");
        var authority = new AuthorityServiceTestHost(table, AuthorityId, [deploymentId]);
        var settings = new ProvisioningAuthoritySettings(
            ProvisioningAuthorityKind.DynamoDb,
            new ProvisioningAuthorityIdentity(deploymentId, AuthorityId),
            DynamoDbTableName: table.TableName,
            DynamoDbRegion: AuthorityServiceTestHost.Region,
            DynamoDbTimeout: TimeSpan.FromSeconds(2));
        var configuration = new Dictionary<string, string>
        {
            ["ProvisioningAuthority:Provider"] = "DynamoDb",
            ["ProvisioningAuthority:DeploymentId"] = deploymentId.ToString(),
            ["ProvisioningAuthority:AuthorityId"] = AuthorityId,
            ["ProvisioningAuthority:DynamoDb:TableName"] = table.TableName,
            ["ProvisioningAuthority:DynamoDb:Region"] = AuthorityServiceTestHost.Region,
            ["ProvisioningAuthority:DynamoDb:TimeoutSeconds"] = "2",
            ["ProvisioningAuthority:AuthorityService:Endpoint"] = AuthorityServiceTestHost.Endpoint.ToString(),
        };
        using var factory = new S01BApiFactory
        {
            UseRealProvisioningVerifier = true,
            Settings = configuration,
            // Chỉ thay AWS client bằng test double read-only; reader/verifier/gate là code thật.
            ConfigureServices = services =>
            {
                services.RemoveAll<IProvisioningAuthorityReader>();
                services.AddSingleton<IProvisioningAuthorityReader>(new DynamoDbProvisioningAuthorityReader(table.CreateClient("runtime", allowWrites: false), settings));
            },
        };
        _ = factory.Services;
        await authority.MustApproveAsync(deploymentId, AuthorityApprovalAction.Bootstrap, null);
        var cliConfiguration = configuration.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
        cliConfiguration["Database:SqlitePath"] = factory.DatabasePath;
        var console = new ScriptedOperatorConsole([InitialPassword, InitialPassword, "BOOTSTRAP"]);
        var exit = await PlatformCliApplication.RunAsync(["bootstrap", "--email", Email, "--display-name", "Owner"], console, new PlatformCliHostOptions
        {
            EnvironmentName = "Testing",
            Configuration = cliConfiguration,
            DynamoDbReadClientFactory = _ => table.CreateClient("cli-read", allowWrites: false),
            AuthorityServiceHandler = authority.CreateGatewayHandler(),
            OperatorCredentials = () => authority.Operator.Credentials,
            PrivilegeCheck = () => true,
        });
        Assert.True(exit == CliExitCodes.Success, console.Output);

        var login = await LoginAsync(factory, InitialPassword);
        using (var me = await factory.SendAsync(HttpMethod.Get, "/api/platform/auth/me", login.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        // Chỉ Authority Service ghi bảng; CLI và runtime chỉ đọc.
        Assert.All(table.Calls.Where(call => call.Operation != "GetItem"), call =>
        {
            Assert.Equal(AuthorityServiceTestHost.ServicePrincipal, call.Principal);
            Assert.Equal("TransactWriteItems", call.Operation);
        });
        var runtimeCalls = table.Calls.Where(call => call.Principal == "runtime").ToList();
        Assert.NotEmpty(runtimeCalls);
        Assert.All(runtimeCalls, call =>
        {
            Assert.Equal("GetItem", call.Operation);
            Assert.True(call.ConsistentRead);
        });

        // Lỗi truy cập authority ở runtime → fail closed.
        table.Fault = (principal, _) => principal == "runtime" ? FakeDynamoDbFault.AccessDenied : FakeDynamoDbFault.None;
        using (var denied = await factory.SendAsync(HttpMethod.Get, "/api/platform/auth/me", login.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
    }

    [Fact]
    public void Api_ExposesNoHttpBootstrapOrRecoveryEndpoint_AndContainsNoAuthorityMutationCode()
    {
        using var factory = new S01BApiFactory { UseRealProvisioningVerifier = true };
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToList();

        Assert.NotEmpty(routes);
        Assert.DoesNotContain(routes, route => new[] { "bootstrap", "recover", "provision", "seal", "authority" }
            .Any(word => route.Contains(word, StringComparison.OrdinalIgnoreCase)));

        var apiAssembly = typeof(PlatformAuthController).Assembly;
        Assert.NotEqual(apiAssembly, typeof(IAuthorityTransitionClient).Assembly);
        Assert.NotEqual(apiAssembly, typeof(IAuthorityStore).Assembly);
        Assert.DoesNotContain(apiAssembly.GetReferencedAssemblies(), reference =>
            reference.Name is "InternalKnowledgeCopilot.AuthorityService" or "InternalKnowledgeCopilot.PlatformCli");
        Assert.DoesNotContain(apiAssembly.GetTypes(), type => type.Name.Contains("Mutator", StringComparison.Ordinal) || type.Name.Contains("AuthorityStore", StringComparison.Ordinal));
        Assert.DoesNotContain(apiAssembly.GetTypes(), type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(method => method.Name is "UpdateItemAsync" or "PutItemAsync" or "TransactWriteItemsAsync"));
        Assert.Null(factory.Services.GetService(typeof(IAuthorityTransitionClient)));
    }

    [Fact]
    public async Task Api_PostToBootstrapLikeRoutes_NeverCreatesPlatformAdmin()
    {
        using var factory = new S01BApiFactory { UseRealProvisioningVerifier = true };
        foreach (var path in new[] { "/api/platform/bootstrap", "/api/platform/auth/bootstrap", "/api/platform/admins", "/api/platform/recover-admin" })
        {
            using var response = await factory.SendAsync(HttpMethod.Post, path, null, new { email = Email, password = InitialPassword });
            Assert.False(response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "application/json", path);
        }

        Assert.Equal(0, await factory.WithDbAsync(db => db.PlatformAdmins.CountAsync()));
        Assert.Equal(0, await factory.WithDbAsync(db => db.PlatformSecurityState.CountAsync()));
    }

    [Fact]
    public async Task CliProcess_StatusExitsOnItsOwnWithoutHostingOrMigrating()
    {
        await using var env = await S01CTestEnvironment.CreateAsync(targetMigration: "20260605010352_ProductHardeningPhase10");
        var before = env.SnapshotDatabase();

        var run = await env.RunHarnessProcessAsync(["status"], [], TimeSpan.FromMinutes(2));

        Assert.Equal(CliExitCodes.Denied, run.ExitCode);
        Assert.Contains("schema_not_current", run.Output);
        Assert.DoesNotContain("Now listening", run.Output);
        Assert.Equal(before, env.SnapshotDatabase());
    }

    private static async Task<PlatformLoginResponse> LoginAsync(S01BApiFactory factory, string password)
    {
        using var response = await factory.PlatformLoginAsync(Email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlatformLoginResponse>())!;
    }

    private sealed class LocalSealHarness : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"ikc-s01c-seal-{Guid.NewGuid():N}");
        private readonly string integrityKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        private readonly Guid deploymentId = Guid.NewGuid();

        public LocalSealHarness()
        {
            Simulator = new LocalSealAuthoritySimulator(new ProvisioningAuthoritySettings(
                ProvisioningAuthorityKind.LocalSeal,
                new ProvisioningAuthorityIdentity(deploymentId, AuthorityId),
                LocalSealPath: SealPath,
                LocalSealIntegrityKey: Convert.FromBase64String(integrityKey)));
        }

        public string SealPath => Path.Combine(directory, "seal.json");

        public LocalSealAuthoritySimulator Simulator { get; }

        public Dictionary<string, string> Configuration => new()
        {
            ["ProvisioningAuthority:Provider"] = "LocalSeal",
            ["ProvisioningAuthority:DeploymentId"] = deploymentId.ToString(),
            ["ProvisioningAuthority:AuthorityId"] = AuthorityId,
            ["ProvisioningAuthority:LocalSeal:Path"] = SealPath,
            ["ProvisioningAuthority:LocalSeal:IntegrityKey"] = integrityKey,
        };

        public S01BApiFactory CreateFactory() => new() { UseRealProvisioningVerifier = true, Settings = Configuration };

        public async Task<CliRun> RunCliAsync(S01BApiFactory factory, string[] args, string[] answers)
        {
            _ = factory.Services;
            var configuration = Configuration.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
            configuration["Database:SqlitePath"] = factory.DatabasePath;
            var console = new ScriptedOperatorConsole(answers);
            var exit = await PlatformCliApplication.RunAsync(args, console, new PlatformCliHostOptions { EnvironmentName = "Testing", Configuration = configuration, PrivilegeCheck = () => true });
            return new CliRun(exit, console.Output);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
