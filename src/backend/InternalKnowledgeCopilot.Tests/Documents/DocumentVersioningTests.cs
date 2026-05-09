using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;
using Xunit;

namespace InternalKnowledgeCopilot.Tests.Documents;

public sealed class DocumentVersioningTests
{
    [Fact]
    public void NewPendingVersion_DoesNotReplaceCurrentVersion()
    {
        var currentVersionId = Guid.NewGuid();
        var pendingVersionId = Guid.NewGuid();
        var document = new DocumentEntity
        {
            Id = Guid.NewGuid(),
            FolderId = Guid.NewGuid(),
            Title = "Policy",
            Status = DocumentStatus.Approved,
            CurrentVersionId = currentVersionId,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Versions =
            [
                CreateVersion(currentVersionId, 1, DocumentVersionStatus.Approved),
                CreateVersion(pendingVersionId, 2, DocumentVersionStatus.PendingReview),
            ],
        };

        Assert.Equal(currentVersionId, document.CurrentVersionId);
        Assert.Contains(document.Versions, version => version.Id == pendingVersionId && version.Status == DocumentVersionStatus.PendingReview);
    }

    private static DocumentVersionEntity CreateVersion(Guid id, int versionNumber, DocumentVersionStatus status)
    {
        return new DocumentVersionEntity
        {
            Id = id,
            DocumentId = Guid.NewGuid(),
            VersionNumber = versionNumber,
            OriginalFileName = "policy.txt",
            StoredFilePath = "storage/policy.txt",
            FileExtension = ".txt",
            FileSizeBytes = 100,
            Status = status,
            UploadedByUserId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
