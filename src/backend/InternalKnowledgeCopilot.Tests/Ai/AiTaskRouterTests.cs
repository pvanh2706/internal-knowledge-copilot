using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.AiProvider;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
using InternalKnowledgeCopilot.Api.Modules.AiSettings;
using Microsoft.EntityFrameworkCore;

namespace InternalKnowledgeCopilot.Tests.Ai;

public sealed class AiTaskRouterTests
{
    [Fact]
    public async Task ResolveAsync_CreatesDefaultPromptTemplateAndSelectsTaskModel()
    {
        await using var dbContext = CreateDbContext();
        var tenantContext = new TenantContext();
        var tenantId = Guid.NewGuid();
        tenantContext.SetTenant(tenantId, "acme");
        var router = new AiTaskRouter(
            dbContext,
            tenantContext,
            new FakeAiProviderSettingsService(new AiProviderOptions
            {
                Name = "openai-compatible",
                ChatModel = "chat-model",
                FastModel = "fast-model",
                EmbeddingProviderName = "embedding-provider",
                EmbeddingModel = "embedding-model"
            }));

        var qaRoute = await router.ResolveAsync(AiTaskType.QuestionAnswering);
        var evalRoute = await router.ResolveAsync(AiTaskType.Evaluation);
        var secondQaRoute = await router.ResolveAsync(AiTaskType.QuestionAnswering);

        Assert.Equal("chat-model", qaRoute.Model);
        Assert.Equal("fast-model", evalRoute.Model);
        Assert.Equal(qaRoute.PromptTemplateId, secondQaRoute.PromptTemplateId);
        Assert.Equal(2, await dbContext.AiPromptTemplates.CountAsync());
        Assert.All(await dbContext.AiPromptTemplates.ToListAsync(), template =>
        {
            Assert.Equal(tenantId, template.TenantId);
            Assert.Equal(1, template.Version);
            Assert.True(template.IsDefault);
            Assert.Equal(AiPromptTemplateStatus.Active, template.Status);
            Assert.False(string.IsNullOrWhiteSpace(template.PromptHash));
        });
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeAiProviderSettingsService(AiProviderOptions options) : IAiProviderSettingsService
    {
        public AiProviderOptions GetCurrent()
        {
            return options;
        }

        public Task<AiProviderOptions> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(options);
        }

        public Task<AiProviderSettingsResponse> GetForAdminAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AiProviderSettingsResponse> UpdateAsync(Guid adminUserId, UpdateAiProviderSettingsRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TestAiProviderSettingsResponse> TestAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
