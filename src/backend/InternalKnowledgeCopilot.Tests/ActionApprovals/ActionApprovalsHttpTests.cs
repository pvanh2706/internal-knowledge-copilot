using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AccessControl;
using InternalKnowledgeCopilot.Api.Infrastructure.ActionExecution;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.ActionApprovals;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static InternalKnowledgeCopilot.Tests.ActionApprovals.ActionRequestTestData;

namespace InternalKnowledgeCopilot.Tests.ActionApprovals;

// Chạy controller, service và connector thật qua pipeline HTTP (Kestrel loopback) để chứng minh endpoint
// trả lỗi policy có kiểm soát (không phải 500) và không có request outbound nào tới hệ thống nguồn.
public sealed class ActionApprovalsHttpTests
{
    [Fact]
    public async Task PostExecute_ReturnsControlledPolicyError_WithoutOutboundRequest()
    {
        await using var host = await ActionApprovalsTestHost.StartAsync(UserRole.Admin);
        var action = await host.SeedActionAsync(AiActionRequestStatus.Approved);

        using var response = await host.Client.PostAsync($"/api/action-approvals/{action.Id}/execute", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal(ExternalActionReadOnlyPolicy.ExecutionBlockedErrorCode, error?.Error);
        Assert.Empty(host.OutboundHandler.Requests);
        await using var dbContext = host.CreateDbContext();
        var stored = await dbContext.AiActionRequests.AsNoTracking().SingleAsync();
        Assert.Equal(AiActionRequestStatus.Approved, stored.Status);
        Assert.Null(stored.ExecutingStartedAt);
        Assert.Null(stored.ExternalExecutionId);
        Assert.Equal(action.UpdatedAt, stored.UpdatedAt);
        var audit = await dbContext.AuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal("AiActionRequestBlocked", audit.Action);
        Assert.Equal(action.Id, audit.EntityId);
        Assert.Equal(host.Seed.User.Id, audit.ActorUserId);
    }

    [Fact]
    public async Task PostCreateWithRuleApproval_ReturnsControlledPolicyError_WithoutOutboundRequest()
    {
        await using var host = await ActionApprovalsTestHost.StartAsync(UserRole.User);

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/action-approvals/recommendations/{host.Seed.Recommendation.Id}/actions",
            new
            {
                actionType = "create_task",
                payloadJson = """{"title":"Schedule follow-up"}""",
                approvalMode = "Rule",
                idempotencyKey = "rule-action-001",
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal(ExternalActionReadOnlyPolicy.ValidationBlockedErrorCode, error?.Error);
        Assert.Empty(host.OutboundHandler.Requests);
        await using var dbContext = host.CreateDbContext();
        Assert.Empty(dbContext.AiActionRequests);
    }

    [Fact]
    public async Task PostExecute_StillRequiresAuthenticatedUser()
    {
        await using var host = await ActionApprovalsTestHost.StartAsync(UserRole.Admin);
        var action = await host.SeedActionAsync(AiActionRequestStatus.Approved);

        using var response = await host.AnonymousClient.PostAsync($"/api/action-approvals/{action.Id}/execute", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(host.OutboundHandler.Requests);
    }

    [Fact]
    public async Task GetActions_ReadOnlyListingStillWorks()
    {
        await using var host = await ActionApprovalsTestHost.StartAsync(UserRole.Reviewer);
        var succeeded = await host.SeedActionAsync(AiActionRequestStatus.Succeeded, item =>
        {
            item.ExternalExecutionId = "external-action-001";
        });

        using var response = await host.Client.GetAsync("/api/action-approvals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actions = await response.Content.ReadFromJsonAsync<List<AiActionRequestResponse>>(ActionApprovalsTestHost.JsonOptions);
        var listed = Assert.Single(actions!);
        Assert.Equal(succeeded.Id, listed.Id);
        Assert.Equal(AiActionRequestStatus.Succeeded, listed.Status);
        Assert.Equal("external-action-001", listed.ExternalExecutionId);
        Assert.Empty(host.OutboundHandler.Requests);
    }

    private sealed class ActionApprovalsTestHost : IAsyncDisposable
    {
        public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly WebApplication app;
        private readonly SqliteConnection connection;

        private ActionApprovalsTestHost(
            WebApplication app,
            SqliteConnection connection,
            ActionSeed seed,
            RecordingHttpMessageHandler outboundHandler,
            Uri baseAddress)
        {
            this.app = app;
            this.connection = connection;
            Seed = seed;
            OutboundHandler = outboundHandler;
            Client = new HttpClient { BaseAddress = baseAddress };
            Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, seed.User.Id.ToString());
            AnonymousClient = new HttpClient { BaseAddress = baseAddress };
        }

        public ActionSeed Seed { get; }

        public RecordingHttpMessageHandler OutboundHandler { get; }

        public HttpClient Client { get; }

        public HttpClient AnonymousClient { get; }

        public static async Task<ActionApprovalsTestHost> StartAsync(UserRole role)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            ActionSeed seed;
            await using (var dbContext = CreateDbContext(connection))
            {
                await dbContext.Database.EnsureCreatedAsync();
                seed = await SeedRecommendationAsync(dbContext, role);
            }

            var outboundHandler = new RecordingHttpMessageHandler();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
                ContentRootPath = AppContext.BaseDirectory,
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(ActionApprovalsController).Assembly)
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddScoped<ITenantContext>(_ =>
            {
                var tenantContext = new TenantContext();
                tenantContext.SetTenant(seed.Tenant.Id, seed.Tenant.Code);
                return tenantContext;
            });
            builder.Services.AddScoped<IAuditLogService, AuditLogService>();
            builder.Services.AddScoped<IActionApprovalRuleService, DefaultActionApprovalRuleService>();
            builder.Services.AddScoped<IActionApprovalService, ActionApprovalService>();
            builder.Services.AddScoped(_ => new InternalHttpExternalConnector(new HttpClient(outboundHandler, disposeHandler: false)));
            builder.Services.AddScoped<IExternalActionExecutor>(services => services.GetRequiredService<InternalHttpExternalConnector>());
            builder.Services.AddScoped<IExternalAccessResolver>(services => services.GetRequiredService<InternalHttpExternalConnector>());

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            return new ActionApprovalsTestHost(app, connection, seed, outboundHandler, new Uri(address));
        }

        public AppDbContext CreateDbContext()
        {
            return CreateDbContext(connection);
        }

        public async Task<AiActionRequestEntity> SeedActionAsync(
            AiActionRequestStatus status,
            Action<AiActionRequestEntity>? configure = null)
        {
            await using var dbContext = CreateDbContext();
            return await ActionRequestTestData.SeedActionAsync(dbContext, Seed, status, configure);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            AnonymousClient.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
            await connection.DisposeAsync();
        }

        private static AppDbContext CreateDbContext(SqliteConnection connection)
        {
            return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string UserHeaderName = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeaderName, out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
