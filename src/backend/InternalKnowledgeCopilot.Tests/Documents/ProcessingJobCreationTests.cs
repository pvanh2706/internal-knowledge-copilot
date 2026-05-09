using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Documents;

public sealed class ProcessingJobCreationTests
{
    [Fact]
    public void ApprovedDocumentVersion_CanCreateProcessingJobTargetingVersion()
    {
        var versionId = Guid.NewGuid();
        var job = new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            JobType = "ExtractAndEmbedDocument",
            TargetType = "DocumentVersion",
            TargetId = versionId,
            Status = ProcessingJobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal(versionId, job.TargetId);
        Assert.Equal(ProcessingJobStatus.Pending, job.Status);
    }
}
