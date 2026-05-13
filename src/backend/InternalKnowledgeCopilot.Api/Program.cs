using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.BackgroundJobs;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.Ai;
using InternalKnowledgeCopilot.Api.Modules.Auth;
using InternalKnowledgeCopilot.Api.Modules.Evaluation;
using InternalKnowledgeCopilot.Api.Modules.Feedback;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using InternalKnowledgeCopilot.Api.Modules.Wiki;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppStorageOptions>(builder.Configuration.GetSection(AppStorageOptions.SectionName));
builder.Services.Configure<ChromaOptions>(builder.Configuration.GetSection(ChromaOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<BackgroundJobOptions>(builder.Configuration.GetSection(BackgroundJobOptions.SectionName));
builder.Services.Configure<AiProviderOptions>(builder.Configuration.GetSection(AiProviderOptions.SectionName));
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IFolderPermissionService, FolderPermissionService>();
builder.Services.AddScoped<IFileUploadValidator, FileUploadValidator>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
builder.Services.AddScoped<IDocumentTextNormalizer, DocumentTextNormalizer>();
builder.Services.AddScoped<ISectionDetector, SectionDetector>();
builder.Services.AddScoped<ITextChunker, TextChunker>();
var aiProviderName = builder.Configuration.GetValue<string>($"{AiProviderOptions.SectionName}:Name") ?? "mock";
if (string.Equals(aiProviderName, "mock", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IEmbeddingService, MockEmbeddingService>();
    builder.Services.AddScoped<IAnswerGenerationService, MockAnswerGenerationService>();
    builder.Services.AddScoped<IWikiDraftGenerationService, MockWikiDraftGenerationService>();
}
else
{
    builder.Services.AddHttpClient<OpenAiCompatibleClient>((serviceProvider, client) =>
    {
        var aiOptions = serviceProvider.GetRequiredService<IOptions<AiProviderOptions>>().Value;
        var baseUrl = aiOptions.BaseUrl.EndsWith("/", StringComparison.Ordinal) ? aiOptions.BaseUrl : aiOptions.BaseUrl + "/";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, aiOptions.TimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(aiOptions.ApiKey))
        {
            if (string.Equals(aiOptions.ApiKeyHeaderName, "api-key", StringComparison.OrdinalIgnoreCase))
            {
                client.DefaultRequestHeaders.Add("api-key", aiOptions.ApiKey);
            }
            else
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiOptions.ApiKey);
            }
        }
    });
    builder.Services.AddScoped<IEmbeddingService, OpenAiCompatibleEmbeddingService>();
    builder.Services.AddScoped<IAnswerGenerationService, OpenAiCompatibleAnswerGenerationService>();
    builder.Services.AddScoped<IWikiDraftGenerationService, OpenAiCompatibleWikiDraftGenerationService>();
}
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IKnowledgeKeywordIndexService, KnowledgeKeywordIndexService>();
builder.Services.AddScoped<IAiQuestionService, AiQuestionService>();
builder.Services.AddScoped<IAiFeedbackService, AiFeedbackService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IWikiService, WikiService>();
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

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

await DevelopmentSeeder.SeedAsync(app.Services, app.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
