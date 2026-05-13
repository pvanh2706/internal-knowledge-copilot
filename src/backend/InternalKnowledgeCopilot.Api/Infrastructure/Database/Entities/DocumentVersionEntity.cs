using InternalKnowledgeCopilot.Api.Common;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

public sealed class DocumentVersionEntity
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public DocumentEntity? Document { get; set; }

    public int VersionNumber { get; set; }

    public required string OriginalFileName { get; set; }

    public required string StoredFilePath { get; set; }

    public required string FileExtension { get; set; }

    public long FileSizeBytes { get; set; }

    public string? ContentType { get; set; }

    public DocumentVersionStatus Status { get; set; }

    public string? RejectReason { get; set; }

    public string? ExtractedTextPath { get; set; }

    public string? NormalizedTextPath { get; set; }

    public int? SectionCount { get; set; }

    public string? ProcessingWarningsJson { get; set; }

    public string? DocumentSummary { get; set; }

    public string? Language { get; set; }

    public string? DocumentType { get; set; }

    public string? KeyTopicsJson { get; set; }

    public string? EntitiesJson { get; set; }

    public DateTimeOffset? EffectiveDate { get; set; }

    public string? Sensitivity { get; set; }

    public string? QualityWarningsJson { get; set; }

    public string? TextHash { get; set; }

    public Guid UploadedByUserId { get; set; }

    public UserEntity? UploadedByUser { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public UserEntity? ReviewedByUser { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset? IndexedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
