using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.BackgroundJobs;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;
using InternalKnowledgeCopilot.Api.Infrastructure.KnowledgeIndex;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.Ai;
using InternalKnowledgeCopilot.Api.Modules.Admin;
using InternalKnowledgeCopilot.Api.Modules.AiSettings;
using InternalKnowledgeCopilot.Api.Modules.Auth;
using InternalKnowledgeCopilot.Api.Modules.Evaluation;
using InternalKnowledgeCopilot.Api.Modules.Feedback;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using InternalKnowledgeCopilot.Api.Modules.KnowledgeIndex;
using InternalKnowledgeCopilot.Api.Modules.Wiki;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppStorageOptions>(builder.Configuration.GetSection(AppStorageOptions.SectionName));
builder.Services.Configure<ChromaOptions>(builder.Configuration.GetSection(ChromaOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
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
builder.Services.AddScoped<IAiProviderSettingsService, AiProviderSettingsService>();
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
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IKnowledgeChunkLedgerService, KnowledgeChunkLedgerService>();
builder.Services.AddScoped<IKnowledgeKeywordIndexService, KnowledgeKeywordIndexService>();
builder.Services.AddScoped<IAiQuestionService, AiQuestionService>();
builder.Services.AddScoped<IAiFeedbackService, AiFeedbackService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IKnowledgeIndexRebuildService, KnowledgeIndexRebuildService>();
builder.Services.AddScoped<IWikiService, WikiService>();
builder.Services.AddScoped<IDataResetService, DataResetService>();
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
