using System.Security.Claims;
using System.Text.Json;
using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.AccessControl;
using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Connectors;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Infrastructure.FileStorage;
using InternalKnowledgeCopilot.Api.Infrastructure.KnowledgeIndex;
using InternalKnowledgeCopilot.Api.Infrastructure.KeywordSearch;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using InternalKnowledgeCopilot.Api.Modules.Ai;
using InternalKnowledgeCopilot.Api.Modules.AuditLogs;
using InternalKnowledgeCopilot.Api.Modules.Auth;
using InternalKnowledgeCopilot.Api.Modules.Documents;
using InternalKnowledgeCopilot.Api.Modules.Evaluation;
using InternalKnowledgeCopilot.Api.Modules.Feedback;
using InternalKnowledgeCopilot.Api.Modules.Folders;
using InternalKnowledgeCopilot.Api.Modules.KnowledgeSources;
using InternalKnowledgeCopilot.Api.Modules.Users;
using InternalKnowledgeCopilot.Api.Modules.Wiki;
using InternalKnowledgeCopilot.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Tenancy;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task TenantScopedReadModels_ReturnOnlyCurrentTenantData()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedTwoTenantsAsync(dbContext);
        var tenantContext = CreateTenantContext(seed.TenantAId);
        var permissionService = new FolderPermissionService(dbContext, tenantContext);

        var usersController = new UsersController(dbContext, tenantContext, new NoopPasswordHasher(), new NoopAuditLogService());
        var usersResult = await usersController.GetUsers(CancellationToken.None);
        var users = ReadOk<IReadOnlyList<UserListItemResponse>>(usersResult);
        Assert.Equal([seed.UserAId], users.Select(user => user.Id).ToArray());

        var visibleFolderIds = await permissionService.GetVisibleFolderIdsAsync(seed.UserAId);
        Assert.Contains(seed.FolderAId, visibleFolderIds);
        Assert.DoesNotContain(seed.FolderBId, visibleFolderIds);

        var foldersController = WithUser(
            new FoldersController(dbContext, tenantContext, permissionService, new NoopAuditLogService()),
            seed.UserAId,
            UserRole.User);
        var foldersResult = await foldersController.GetTree(CancellationToken.None);
        var folders = ReadOk<IReadOnlyList<FolderTreeItemResponse>>(foldersResult);
        Assert.Equal([seed.FolderAId], folders.Select(folder => folder.Id).ToArray());

        var documentsController = WithUser(
            new DocumentsController(
                dbContext,
                tenantContext,
                permissionService,
                new NoopFileUploadValidator(),
                new NoopFileStorageService(),
                new FakeKnowledgeSourceService(),
                new FakeProcessingJobService(),
                new NoopAuditLogService()),
            seed.UserAId,
            UserRole.User);
        var documentsResult = await documentsController.GetDocuments(null, null, null, CancellationToken.None);
        var documents = ReadOk<IReadOnlyList<DocumentListItemResponse>>(documentsResult);
        Assert.Equal([seed.DocumentAId], documents.Select(document => document.Id).ToArray());

        var wikiDrafts = await CreateWikiService(dbContext, tenantContext, permissionService).GetDraftsAsync();
        Assert.Equal([seed.WikiDraftAId], wikiDrafts.Select(draft => draft.Id).ToArray());

        var evaluationCases = await new EvaluationService(
            dbContext,
            tenantContext,
            new FakeAiQuestionService(),
            new NoopAuditLogService()).GetCasesAsync();
        Assert.Equal([seed.EvaluationCaseAId], evaluationCases.Select(evaluationCase => evaluationCase.Id).ToArray());

        var auditController = new AuditLogsController(dbContext, tenantContext);
        var auditResult = await auditController.GetLogs(null, null, null, null, CancellationToken.None);
        var logs = ReadOk<IReadOnlyList<AuditLogResponse>>(auditResult);
        Assert.Equal([seed.AuditLogAId], logs.Select(log => log.Id).ToArray());
    }

    [Fact]
    public async Task AiQuestionAsync_DoesNotCiteOtherTenantVectorCandidate()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedTwoTenantsAsync(dbContext);
        var tenantContext = CreateTenantContext(seed.TenantAId);
        var vectorStore = new FakeKnowledgeVectorStore([
            CreateVectorResult(
                "tenant-b-spoofed-folder",
                seed.FolderAId,
                seed.DocumentBId,
                seed.DocumentVersionBId,
                "Tenant B secret",
                "/Tenant A",
                "payment escalation secret from tenant b"),
            CreateVectorResult(
                "tenant-a",
                seed.FolderAId,
                seed.DocumentAId,
                seed.DocumentVersionAId,
                "Tenant A policy",
                "/Tenant A",
                "payment escalation policy from tenant a"),
        ]);
        var service = new AiQuestionService(
            dbContext,
            tenantContext,
            new FolderPermissionService(dbContext, tenantContext),
            new MockEmbeddingService(),
            vectorStore,
            new KnowledgeKeywordIndexService(dbContext),
            new AllowingExternalAccessResolver(),
            new MockAnswerGenerationService());

        var response = await service.AskAsync(
            seed.UserAId,
            new AskQuestionRequest("payment escalation", AiScopeType.All, null, null));

        Assert.NotNull(vectorStore.LastFilter);
        Assert.Equal(seed.TenantAId, vectorStore.LastFilter.TenantId);
        var citation = Assert.Single(response.Citations);
        Assert.Equal("Tenant A policy", citation.Title);
        Assert.DoesNotContain("tenant b", response.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FeedbackService_DoesNotAcceptOtherTenantInteraction()
    {
        await using var dbContext = CreateDbContext();
        var seed = await SeedTwoTenantsAsync(dbContext);
        var tenantContext = CreateTenantContext(seed.TenantAId);
        var service = new AiFeedbackService(
            dbContext,
            tenantContext,
            new NoopAuditLogService(),
            new MockEmbeddingService(),
            new FakeKnowledgeVectorStore([]),
            new KnowledgeChunkLedgerService(dbContext),
            new KnowledgeKeywordIndexService(dbContext));

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SubmitAsync(
            seed.OtherTenantInteractionId,
            seed.UserAId,
            new SubmitFeedbackRequest(AiFeedbackValue.Incorrect, "Wrong tenant"),
            CancellationToken.None));

        Assert.Equal("interaction_not_found", exception.Message);
    }

    [Fact]
    public async Task TenantResolutionMiddleware_RejectsHeaderMismatchForAuthenticatedToken()
    {
        await using var dbContext = CreateDbContext();
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.Tenants.AddRange(
            new TenantEntity { Id = tenantAId, Name = "Tenant A", Code = "tenant-a", Status = TenantStatus.Active, CreatedAt = now, UpdatedAt = now },
            new TenantEntity { Id = tenantBId, Name = "Tenant B", Code = "tenant-b", Status = TenantStatus.Active, CreatedAt = now, UpdatedAt = now });
        await dbContext.SaveChangesAsync();

        var tenantContext = new TenantContext();
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/documents";
        httpContext.Request.Headers["X-Tenant-Code"] = "tenant-a";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(TenantClaimTypes.TenantCode, "tenant-b"),
        ], "Test"));

        await middleware.InvokeAsync(httpContext, dbContext, tenantContext);

        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);
        Assert.False(nextCalled);
        Assert.False(tenantContext.HasTenant);
    }

    private static async Task<TwoTenantSeed> SeedTwoTenantsAsync(AppDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var folderAId = Guid.NewGuid();
        var folderBId = Guid.NewGuid();
        var documentAId = Guid.NewGuid();
        var documentBId = Guid.NewGuid();
        var documentVersionAId = Guid.NewGuid();
        var documentVersionBId = Guid.NewGuid();
        var wikiDraftAId = Guid.NewGuid();
        var wikiDraftBId = Guid.NewGuid();
        var evaluationCaseAId = Guid.NewGuid();
        var evaluationCaseBId = Guid.NewGuid();
        var auditLogAId = Guid.NewGuid();
        var auditLogBId = Guid.NewGuid();
        var otherTenantInteractionId = Guid.NewGuid();

        dbContext.Teams.AddRange(
            new TeamEntity { Id = teamAId, TenantId = tenantAId, Name = "Team A", CreatedAt = now, UpdatedAt = now },
            new TeamEntity { Id = teamBId, TenantId = tenantBId, Name = "Team B", CreatedAt = now, UpdatedAt = now });
        dbContext.Users.AddRange(
            new UserEntity
            {
                Id = userAId,
                TenantId = tenantAId,
                Email = "user-a@example.local",
                DisplayName = "User A",
                PasswordHash = "hash",
                Role = UserRole.User,
                PrimaryTeamId = teamAId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new UserEntity
            {
                Id = userBId,
                TenantId = tenantBId,
                Email = "user-b@example.local",
                DisplayName = "User B",
                PasswordHash = "hash",
                Role = UserRole.User,
                PrimaryTeamId = teamBId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
        dbContext.Folders.AddRange(
            new FolderEntity { Id = folderAId, TenantId = tenantAId, Name = "Tenant A", Path = "/Tenant A", CreatedByUserId = userAId, CreatedAt = now, UpdatedAt = now },
            new FolderEntity { Id = folderBId, TenantId = tenantBId, Name = "Tenant B", Path = "/Tenant B", CreatedByUserId = userBId, CreatedAt = now, UpdatedAt = now });
        dbContext.FolderPermissions.AddRange(
            new FolderPermissionEntity { Id = Guid.NewGuid(), TenantId = tenantAId, FolderId = folderAId, TeamId = teamAId, CanView = true, CreatedAt = now, UpdatedAt = now },
            new FolderPermissionEntity { Id = Guid.NewGuid(), TenantId = tenantBId, FolderId = folderBId, TeamId = teamBId, CanView = true, CreatedAt = now, UpdatedAt = now });
        dbContext.Documents.AddRange(
            CreateDocument(documentAId, tenantAId, folderAId, documentVersionAId, userAId, "Tenant A policy", now),
            CreateDocument(documentBId, tenantBId, folderBId, documentVersionBId, userBId, "Tenant B secret", now));
        dbContext.DocumentVersions.AddRange(
            CreateDocumentVersion(documentVersionAId, tenantAId, documentAId, userAId, now),
            CreateDocumentVersion(documentVersionBId, tenantBId, documentBId, userBId, now));
        dbContext.WikiDrafts.AddRange(
            CreateWikiDraft(wikiDraftAId, tenantAId, documentAId, documentVersionAId, userAId, "Tenant A wiki", now),
            CreateWikiDraft(wikiDraftBId, tenantBId, documentBId, documentVersionBId, userBId, "Tenant B wiki", now));
        dbContext.EvaluationCases.AddRange(
            CreateEvaluationCase(evaluationCaseAId, tenantAId, userAId, "Tenant A eval", now),
            CreateEvaluationCase(evaluationCaseBId, tenantBId, userBId, "Tenant B eval", now));
        dbContext.AuditLogs.AddRange(
            new AuditLogEntity { Id = auditLogAId, TenantId = tenantAId, ActorUserId = userAId, Action = "TenantAAction", EntityType = "Test", CreatedAt = now },
            new AuditLogEntity { Id = auditLogBId, TenantId = tenantBId, ActorUserId = userBId, Action = "TenantBAction", EntityType = "Test", CreatedAt = now });
        dbContext.AiInteractions.Add(new AiInteractionEntity
        {
            Id = otherTenantInteractionId,
            TenantId = tenantBId,
            UserId = userAId,
            Question = "Wrong tenant question",
            Answer = "Wrong tenant answer",
            ScopeType = AiScopeType.All,
            CreatedAt = now,
        });

        await dbContext.SaveChangesAsync();
        return new TwoTenantSeed(
            tenantAId,
            tenantBId,
            userAId,
            folderAId,
            folderBId,
            documentAId,
            documentBId,
            documentVersionAId,
            documentVersionBId,
            wikiDraftAId,
            evaluationCaseAId,
            auditLogAId,
            otherTenantInteractionId);
    }

    private static DocumentEntity CreateDocument(
        Guid id,
        Guid tenantId,
        Guid folderId,
        Guid versionId,
        Guid userId,
        string title,
        DateTimeOffset now)
    {
        return new DocumentEntity
        {
            Id = id,
            TenantId = tenantId,
            FolderId = folderId,
            Title = title,
            Status = DocumentStatus.Approved,
            CurrentVersionId = versionId,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static DocumentVersionEntity CreateDocumentVersion(
        Guid id,
        Guid tenantId,
        Guid documentId,
        Guid userId,
        DateTimeOffset now)
    {
        return new DocumentVersionEntity
        {
            Id = id,
            TenantId = tenantId,
            DocumentId = documentId,
            VersionNumber = 1,
            OriginalFileName = "source.txt",
            StoredFilePath = "source.txt",
            FileExtension = ".txt",
            FileSizeBytes = 10,
            Status = DocumentVersionStatus.Indexed,
            UploadedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static WikiDraftEntity CreateWikiDraft(
        Guid id,
        Guid tenantId,
        Guid documentId,
        Guid versionId,
        Guid userId,
        string title,
        DateTimeOffset now)
    {
        return new WikiDraftEntity
        {
            Id = id,
            TenantId = tenantId,
            SourceDocumentId = documentId,
            SourceDocumentVersionId = versionId,
            Title = title,
            Content = $"# {title}",
            Language = "en",
            MissingInformationJson = JsonSerializer.Serialize(Array.Empty<string>()),
            RelatedDocumentsJson = JsonSerializer.Serialize(Array.Empty<WikiRelatedDocumentResponse>()),
            Status = WikiStatus.Draft,
            GeneratedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static EvaluationCaseEntity CreateEvaluationCase(
        Guid id,
        Guid tenantId,
        Guid userId,
        string question,
        DateTimeOffset now)
    {
        return new EvaluationCaseEntity
        {
            Id = id,
            TenantId = tenantId,
            Question = question,
            ExpectedAnswer = "Expected answer",
            ExpectedKeywordsJson = JsonSerializer.Serialize(new[] { "expected" }),
            ScopeType = AiScopeType.All,
            CreatedByUserId = userId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static KnowledgeVectorSearchResult CreateVectorResult(
        string id,
        Guid folderId,
        Guid documentId,
        Guid documentVersionId,
        string title,
        string folderPath,
        string text)
    {
        return new KnowledgeVectorSearchResult(
            id,
            text,
            new Dictionary<string, object?>
            {
                ["source_type"] = "document",
                ["source_id"] = documentVersionId.ToString(),
                ["document_id"] = documentId.ToString(),
                ["document_version_id"] = documentVersionId.ToString(),
                ["folder_id"] = folderId.ToString(),
                ["title"] = title,
                ["folder_path"] = folderPath,
                ["section_title"] = "Main",
                ["section_index"] = 1,
            },
            0.1);
    }

    private static IWikiService CreateWikiService(
        AppDbContext dbContext,
        ITenantContext tenantContext,
        IFolderPermissionService permissionService)
    {
        return new WikiService(
            dbContext,
            tenantContext,
            permissionService,
            new MockWikiDraftGenerationService(),
            new TextChunker(),
            new SectionDetector(),
            new MockEmbeddingService(),
            new FakeKnowledgeVectorStore([]),
            new KnowledgeChunkLedgerService(dbContext),
            new KnowledgeKeywordIndexService(dbContext),
            new FakeKnowledgeSourceService(),
            new NoopAuditLogService());
    }

    private static TController WithUser<TController>(TController controller, Guid userId, UserRole role)
        where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, role.ToString()),
                ], "Test")),
            },
        };
        return controller;
    }

    private static T ReadOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static TenantContext CreateTenantContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId, "tenant-a");
        return tenantContext;
    }

    private sealed record TwoTenantSeed(
        Guid TenantAId,
        Guid TenantBId,
        Guid UserAId,
        Guid FolderAId,
        Guid FolderBId,
        Guid DocumentAId,
        Guid DocumentBId,
        Guid DocumentVersionAId,
        Guid DocumentVersionBId,
        Guid WikiDraftAId,
        Guid EvaluationCaseAId,
        Guid AuditLogAId,
        Guid OtherTenantInteractionId);

    private sealed class NoopPasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => password;

        public bool VerifyPassword(string passwordHash, string password) => passwordHash == password;
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task RecordAsync(Guid? actorUserId, string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoopFileUploadValidator : IFileUploadValidator
    {
        public FileValidationResult Validate(IFormFile? file) => FileValidationResult.Valid();
    }

    private sealed class NoopFileStorageService : IFileStorageService
    {
        public Task<string> SaveDocumentVersionAsync(Guid documentId, Guid versionId, IFormFile file, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("stored.txt");
        }

        public bool TryResolveStoredPath(string storedPath, out string resolvedPath)
        {
            resolvedPath = storedPath;
            return true;
        }
    }

    private sealed class FakeAiQuestionService : IAiQuestionService
    {
        public Task<AskQuestionResponse> AskAsync(Guid userId, AskQuestionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AskQuestionResponse(Guid.NewGuid(), "Answer", false, "high", [], [], [], []));
        }

        public Task<RetrievalExplainResponse> ExplainRetrievalAsync(Guid userId, AskQuestionRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeKnowledgeSourceService : IKnowledgeSourceService
    {
        public Task<IReadOnlyList<KnowledgeSourceResponse>> GetSourcesAsync(Guid? applicationId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<KnowledgeSourceResponse>>([]);
        }

        public Task<KnowledgeSourceResponse> UpsertSourceAsync(Guid? actorUserId, UpsertKnowledgeSourceRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ExternalObjectResponse>> GetExternalObjectsAsync(Guid? applicationId = null, Guid? knowledgeSourceId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ExternalObjectResponse>>([]);
        }

        public Task<ExternalObjectResponse> UpsertExternalObjectAsync(Guid? actorUserId, UpsertExternalObjectRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ExternalAclSnapshotResponse>> ReplaceAclSnapshotsAsync(Guid? actorUserId, ReplaceExternalAclSnapshotsRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<KnowledgeSourceEntity> GetOrCreateDefaultLocalSourceAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new KnowledgeSourceEntity
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                ApplicationId = Guid.NewGuid(),
                SourceType = KnowledgeSourceKind.Local,
                ExternalSourceId = "local",
                Name = "Local",
                SyncMode = KnowledgeSourceSyncMode.Manual,
                Status = KnowledgeSourceStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private sealed class AllowingExternalAccessResolver : IExternalAccessResolver
    {
        public Task<ExternalAccessCheckResponse> CheckAccessAsync(
            ExternalConnectorContext context,
            ExternalAccessCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ExternalAccessCheckResponse(true, null, DateTimeOffset.UtcNow));
        }
    }

    private sealed class FakeKnowledgeVectorStore(IReadOnlyList<KnowledgeVectorSearchResult> results) : IKnowledgeVectorStore
    {
        public KnowledgeQueryFilter? LastFilter { get; private set; }

        public Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResetCollectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteTenantDataAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpsertChunksAsync(IReadOnlyList<KnowledgeChunkRecord> chunks, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KnowledgeVectorSearchResult>> QueryAsync(
            float[] embedding,
            int limit,
            KnowledgeQueryFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult(results);
        }
    }
}
