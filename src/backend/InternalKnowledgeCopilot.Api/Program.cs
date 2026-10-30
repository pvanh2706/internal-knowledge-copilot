using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.AccessControl;
using InternalKnowledgeCopilot.Api.Infrastructure.Authentication;
using InternalKnowledgeCopilot.Api.Infrastructure.ActionExecution;
using InternalKnowledgeCopilot.Api.Infrastructure.BackgroundJobs;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;
using InternalKnowledgeCopilot.Api.Infrastructure.KnowledgeIndex;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.Ai;
using InternalKnowledgeCopilot.Api.Modules.ActionApprovals;
using InternalKnowledgeCopilot.Api.Modules.Admin;
using InternalKnowledgeCopilot.Api.Modules.AiSettings;
using InternalKnowledgeCopilot.Api.Modules.Applications;
using InternalKnowledgeCopilot.Api.Modules.Auth;
using InternalKnowledgeCopilot.Api.Modules.Evaluation;
using InternalKnowledgeCopilot.Api.Modules.Feedback;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using InternalKnowledgeCopilot.Api.Modules.KnowledgeIndex;
using InternalKnowledgeCopilot.Api.Modules.KnowledgeSources;
using InternalKnowledgeCopilot.Api.Modules.Integrations;
using InternalKnowledgeCopilot.Api.Modules.Operations;
using InternalKnowledgeCopilot.Api.Modules.Platform.Auth;
using InternalKnowledgeCopilot.Api.Modules.Tenants;
using InternalKnowledgeCopilot.Api.Modules.WorkflowCopilot;
using InternalKnowledgeCopilot.Api.Modules.Wiki;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.ErrorHandling;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppStorageOptions>(builder.Configuration.GetSection(AppStorageOptions.SectionName));
builder.Services.Configure<ChromaOptions>(builder.Configuration.GetSection(ChromaOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<PlatformJwtOptions>(builder.Configuration.GetSection(PlatformJwtOptions.SectionName));
builder.Services.Configure<PlatformAuthOptions>(builder.Configuration.GetSection(PlatformAuthOptions.SectionName));
builder.Services.Configure<BackgroundJobOptions>(builder.Configuration.GetSection(BackgroundJobOptions.SectionName));
builder.Services.Configure<AiProviderOptions>(builder.Configuration.GetSection(AiProviderOptions.SectionName));
builder.Services.Configure<DataResetOptions>(builder.Configuration.GetSection(DataResetOptions.SectionName));
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<ApiErrorExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IApiKeyProtector, DataProtectionApiKeyProtector>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IPlatformPasswordHasher, PlatformPasswordHasher>();
builder.Services.AddSingleton<IPlatformJwtTokenService, PlatformJwtTokenService>();
builder.Services.AddScoped<IPlatformAuditWriter, PlatformAuditWriter>();
// Provisioning chưa được S01-C xác minh: mặc định luôn từ chối, không có cờ cấu hình nào bỏ qua được.
builder.Services.AddSingleton<IPlatformProvisioningVerifier, UnverifiedPlatformProvisioningVerifier>();
builder.Services.AddScoped<IPlatformProvisioningGate, PlatformProvisioningGate>();
builder.Services.AddScoped<IPlatformAuthService, PlatformAuthService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IFolderPermissionService, FolderPermissionService>();
builder.Services.AddScoped<IFileUploadValidator, FileUploadValidator>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
builder.Services.AddScoped<IDocumentTextNormalizer, DocumentTextNormalizer>();
builder.Services.AddScoped<ISectionDetector, SectionDetector>();
builder.Services.AddScoped<ITextChunker, TextChunker>();
builder.Services.AddScoped<IAiProviderSettingsService, AiProviderSettingsService>();
builder.Services.AddScoped<IAiTaskRouter, AiTaskRouter>();
builder.Services.AddHttpClient<OpenAiCompatibleClient>();
builder.Services.AddScoped<MockEmbeddingService>();
builder.Services.AddScoped<OpenAiCompatibleEmbeddingService>();
builder.Services.AddScoped<IEmbeddingService, RuntimeEmbeddingService>();
builder.Services.AddScoped<MockAnswerGenerationService>();
builder.Services.AddScoped<OpenAiCompatibleAnswerGenerationService>();
builder.Services.AddScoped<IAnswerGenerationService, RuntimeAnswerGenerationService>();
builder.Services.AddScoped<MockWikiDraftGenerationService>();
builder.Services.AddScoped<OpenAiCompatibleWikiDraftGenerationService>();
builder.Services.AddScoped<IWikiDraftGenerationService, RuntimeWikiDraftGenerationService>();
builder.Services.AddScoped<MockDocumentUnderstandingService>();
builder.Services.AddScoped<OpenAiCompatibleDocumentUnderstandingService>();
builder.Services.AddScoped<IDocumentUnderstandingService, RuntimeDocumentUnderstandingService>();
builder.Services.AddScoped<MockWorkflowRecommendationGenerationService>();
builder.Services.AddScoped<OpenAiCompatibleWorkflowRecommendationGenerationService>();
builder.Services.AddScoped<IWorkflowRecommendationGenerationService, RuntimeWorkflowRecommendationGenerationService>();
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IKnowledgeChunkLedgerService, KnowledgeChunkLedgerService>();
builder.Services.AddScoped<IKnowledgeKeywordIndexService, KnowledgeKeywordIndexService>();
builder.Services.AddScoped<IAiQuestionService, AiQuestionService>();
builder.Services.AddScoped<IAiFeedbackService, AiFeedbackService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IKnowledgeIndexRebuildService, KnowledgeIndexRebuildService>();
builder.Services.AddScoped<IKnowledgeSourceService, KnowledgeSourceService>();
builder.Services.AddSingleton<IIntegrationSecretHasher, IntegrationSecretHasher>();
builder.Services.AddScoped<IIntegrationService, IntegrationService>();
builder.Services.AddScoped<IWorkflowCopilotService, WorkflowCopilotService>();
builder.Services.AddScoped<IActionApprovalRuleService, DefaultActionApprovalRuleService>();
builder.Services.AddScoped<IActionApprovalService, ActionApprovalService>();
builder.Services.AddScoped<IProcessingJobService, ProcessingJobService>();
builder.Services.AddScoped<IOperationalMetricsService, OperationalMetricsService>();
builder.Services.AddScoped<IWikiService, WikiService>();
builder.Services.AddScoped<IDataResetService, DataResetService>();
builder.Services.AddHttpClient<IExternalContentClient, InternalHttpExternalConnector>();
builder.Services.AddHttpClient<IExternalObjectContextClient, InternalHttpExternalConnector>();
builder.Services.AddHttpClient<IExternalAccessResolver, InternalHttpExternalConnector>();
builder.Services.AddHttpClient<IExternalActionExecutor, InternalHttpExternalConnector>();
builder.Services.AddHttpClient<IKnowledgeVectorStore, ChromaKnowledgeVectorStore>((serviceProvider, client) =>
{
    var chromaOptions = serviceProvider.GetRequiredService<IOptions<ChromaOptions>>().Value;
    client.BaseAddress = new Uri(chromaOptions.BaseUrl);
});
builder.Services.AddHostedService<ProcessingJobWorker>();

var sqlitePath = builder.Configuration.GetValue<string>("Database:SqlitePath")
    ?? Path.Combine(AppContext.BaseDirectory, "data", "internal-knowledge-copilot.db");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sqlitePath))!);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={sqlitePath}");
});

// Key ring Data Protection phải nằm trên đĩa để API key mã hóa trong DB vẫn giải mã được sau khi redeploy.
var dataProtectionKeysPath = builder.Configuration.GetValue<string>("DataProtection:KeysPath")
    ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sqlitePath))!, "dataprotection-keys");
Directory.CreateDirectory(dataProtectionKeysPath);
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("InternalKnowledgeCopilot");
if (OperatingSystem.IsWindows())
{
    dataProtectionBuilder.ProtectKeysWithDpapi();
}

var rateLimitingOptions = builder.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
    ?? new RateLimitingOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ApiError("rate_limited", "Quá nhiều yêu cầu. Vui lòng thử lại sau."),
            cancellationToken);
    };
    // Endpoint AI giới hạn theo user vì mỗi request có thể tốn chi phí LLM/embedding.
    options.AddPolicy("ai", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitingOptions.AiRequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    // Endpoint tích hợp giới hạn theo integration key để một đối tác lỗi không làm nghẽn đối tác khác.
    options.AddPolicy("integrations", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Request.Headers["X-Integration-Key"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitingOptions.IntegrationRequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    // Đăng nhập platform giới hạn theo IP, bổ sung cho khóa tài khoản theo số lần sai và giới hạn chi phí Argon2id.
    options.AddPolicy(PlatformAuthController.RateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitingOptions.PlatformAuthRequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// S01-B: hai scheme độc lập TenantJwt/PlatformJwt; cấu hình thiếu hoặc không an toàn thì chặn khởi động.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var platformJwtOptions = builder.Configuration.GetSection(PlatformJwtOptions.SectionName).Get<PlatformJwtOptions>() ?? new PlatformJwtOptions();
JwtConfigurationValidator.Validate(jwtOptions, platformJwtOptions, builder.Environment.IsProduction());
builder.Services.AddAppAuthentication(jwtOptions, platformJwtOptions);

var app = builder.Build();

// Production chỉ migrate khi bật cờ tường minh để installer/update script kiểm soát được thời điểm migrate.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Dữ liệu demo tuyệt đối không được seed vào Production.
if (!app.Environment.IsProduction())
{
    await DevelopmentSeeder.SeedAsync(app.Services, app.Configuration);
}

await AiProviderKeyStartupMigration.RunAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseCors();
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
if (rateLimitingOptions.Enabled)
{
    app.UseRateLimiter();
}

app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
