using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Tenancy;

// Kiểm tra lưới an toàn global query filter theo tenant trong AppDbContext:
// truy vấn KHÔNG có Where tenant thủ công vẫn phải bị lọc đúng tenant của scope.
public sealed class TenantQueryFilterTests
{
    private static readonly Guid TenantAId = Guid.NewGuid();
    private static readonly Guid TenantBId = Guid.NewGuid();

    [Fact]
    public async Task Documents_TenantA_KhongThayDuLieuTenantB()
    {
        var options = CreateOptions();
        await SeedAsync(options);

        await using var dbContext = CreateTenantScopedContext(options, TenantAId);
        var documents = await dbContext.Documents.ToListAsync();

        Assert.Single(documents);
        Assert.All(documents, document => Assert.Equal(TenantAId, document.TenantId));
    }

    [Fact]
    public async Task WikiPages_TenantA_KhongThayDuLieuTenantB()
    {
        var options = CreateOptions();
        await SeedAsync(options);

        await using var dbContext = CreateTenantScopedContext(options, TenantAId);
        var wikiPages = await dbContext.WikiPages.ToListAsync();

        Assert.Single(wikiPages);
        Assert.All(wikiPages, page => Assert.Equal(TenantAId, page.TenantId));
    }

    [Fact]
    public async Task AiInteractions_TenantA_KhongThayDuLieuTenantB()
    {
        var options = CreateOptions();
        await SeedAsync(options);

        await using var dbContext = CreateTenantScopedContext(options, TenantAId);
        var interactions = await dbContext.AiInteractions.ToListAsync();

        Assert.Single(interactions);
        Assert.All(interactions, interaction => Assert.Equal(TenantAId, interaction.TenantId));
    }

    [Fact]
    public async Task TimTheoIdCuaTenantKhac_TraVeNull()
    {
        var options = CreateOptions();
        var seed = await SeedAsync(options);

        await using var dbContext = CreateTenantScopedContext(options, TenantAId);
        var documentB = await dbContext.Documents.FirstOrDefaultAsync(document => document.Id == seed.DocumentBId);

        Assert.Null(documentB);
    }

    [Fact]
    public async Task ScopeKhongCoTenant_ThayToanBoDuLieu()
    {
        var options = CreateOptions();
        await SeedAsync(options);

        // Background worker/seeder tạo AppDbContext không có tenant context nên phải thấy mọi tenant.
        await using var dbContext = new AppDbContext(options);
        var documents = await dbContext.Documents.ToListAsync();

        Assert.Equal(2, documents.Count);
    }

    [Fact]
    public async Task ScopeCoTenantContextNhungChuaSetTenant_KhongLoc()
    {
        var options = CreateOptions();
        await SeedAsync(options);

        await using var dbContext = new AppDbContext(options, new TenantContext());
        var documents = await dbContext.Documents.ToListAsync();

        Assert.Equal(2, documents.Count);
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    private static AppDbContext CreateTenantScopedContext(DbContextOptions<AppDbContext> options, Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId, "tenant-a");
        return new AppDbContext(options, tenantContext);
    }

    private static async Task<(Guid DocumentAId, Guid DocumentBId)> SeedAsync(DbContextOptions<AppDbContext> options)
    {
        await using var dbContext = new AppDbContext(options);
        var documentAId = Guid.NewGuid();
        var documentBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        dbContext.Documents.AddRange(
            CreateDocument(documentAId, TenantAId, now),
            CreateDocument(documentBId, TenantBId, now));
        dbContext.WikiPages.AddRange(
            CreateWikiPage(TenantAId, documentAId, now),
            CreateWikiPage(TenantBId, documentBId, now));
        dbContext.AiInteractions.AddRange(
            CreateInteraction(TenantAId, now),
            CreateInteraction(TenantBId, now));

        await dbContext.SaveChangesAsync();
        return (documentAId, documentBId);
    }

    private static DocumentEntity CreateDocument(Guid id, Guid tenantId, DateTimeOffset now)
    {
        return new DocumentEntity
        {
            Id = id,
            TenantId = tenantId,
            FolderId = Guid.NewGuid(),
            Title = "Tài liệu kiểm tra",
            Status = DocumentStatus.PendingReview,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static WikiPageEntity CreateWikiPage(Guid tenantId, Guid sourceDocumentId, DateTimeOffset now)
    {
        return new WikiPageEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceDraftId = Guid.NewGuid(),
            SourceDocumentId = sourceDocumentId,
            SourceDocumentVersionId = Guid.NewGuid(),
            Title = "Wiki kiểm tra",
            Content = "Nội dung",
            Language = "vi",
            VisibilityScope = VisibilityScope.Company,
            PublishedByUserId = Guid.NewGuid(),
            PublishedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static AiInteractionEntity CreateInteraction(Guid tenantId, DateTimeOffset now)
    {
        return new AiInteractionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            Question = "Câu hỏi kiểm tra",
            Answer = "Câu trả lời kiểm tra",
            ScopeType = AiScopeType.All,
            CreatedAt = now,
        };
    }
}
