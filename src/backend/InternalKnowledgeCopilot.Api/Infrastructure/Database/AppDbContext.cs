using Microsoft.EntityFrameworkCore;
using InternalKnowledgeCopilot.Api.Infrastructure.Database.Entities;

namespace InternalKnowledgeCopilot.Api.Infrastructure.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<TeamEntity> Teams => Set<TeamEntity>();

    public DbSet<FolderEntity> Folders => Set<FolderEntity>();

    public DbSet<FolderPermissionEntity> FolderPermissions => Set<FolderPermissionEntity>();

    public DbSet<UserFolderPermissionEntity> UserFolderPermissions => Set<UserFolderPermissionEntity>();

    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();

    public DbSet<DocumentVersionEntity> DocumentVersions => Set<DocumentVersionEntity>();

    public DbSet<ProcessingJobEntity> ProcessingJobs => Set<ProcessingJobEntity>();

    public DbSet<AiInteractionEntity> AiInteractions => Set<AiInteractionEntity>();

    public DbSet<AiInteractionSourceEntity> AiInteractionSources => Set<AiInteractionSourceEntity>();

    public DbSet<AiFeedbackEntity> AiFeedback => Set<AiFeedbackEntity>();

    public DbSet<AiQualityIssueEntity> AiQualityIssues => Set<AiQualityIssueEntity>();

    public DbSet<KnowledgeCorrectionEntity> KnowledgeCorrections => Set<KnowledgeCorrectionEntity>();

    public DbSet<RetrievalHintEntity> RetrievalHints => Set<RetrievalHintEntity>();

    public DbSet<KnowledgeChunkIndexEntity> KnowledgeChunkIndexes => Set<KnowledgeChunkIndexEntity>();

    public DbSet<EvaluationCaseEntity> EvaluationCases => Set<EvaluationCaseEntity>();

    public DbSet<EvaluationRunEntity> EvaluationRuns => Set<EvaluationRunEntity>();

    public DbSet<EvaluationRunResultEntity> EvaluationRunResults => Set<EvaluationRunResultEntity>();

    public DbSet<WikiDraftEntity> WikiDrafts => Set<WikiDraftEntity>();

    public DbSet<WikiPageEntity> WikiPages => Set<WikiPageEntity>();

    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TeamEntity>(entity =>
        {
            entity.ToTable("teams");
            entity.HasKey(team => team.Id);
            entity.Property(team => team.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(team => team.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(team => team.CreatedAt).HasColumnName("created_at");
            entity.Property(team => team.UpdatedAt).HasColumnName("updated_at");
            entity.Property(team => team.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(team => team.Name).IsUnique();
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(user => user.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(50);
            entity.Property(user => user.PrimaryTeamId).HasColumnName("primary_team_id");
            entity.Property(user => user.MustChangePassword).HasColumnName("must_change_password");
            entity.Property(user => user.IsActive).HasColumnName("is_active");
            entity.Property(user => user.CreatedAt).HasColumnName("created_at");
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at");
            entity.Property(user => user.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasOne(user => user.PrimaryTeam)
                .WithMany(team => team.Users)
                .HasForeignKey(user => user.PrimaryTeamId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FolderEntity>(entity =>
        {
            entity.ToTable("folders");
            entity.HasKey(folder => folder.Id);
            entity.Property(folder => folder.ParentId).HasColumnName("parent_id");
            entity.Property(folder => folder.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(folder => folder.Path).HasColumnName("path").HasMaxLength(1000).IsRequired();
            entity.Property(folder => folder.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(folder => folder.CreatedAt).HasColumnName("created_at");
            entity.Property(folder => folder.UpdatedAt).HasColumnName("updated_at");
            entity.Property(folder => folder.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(folder => folder.Path).IsUnique();
            entity.HasOne(folder => folder.Parent)
                .WithMany(folder => folder.Children)
                .HasForeignKey(folder => folder.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(folder => folder.CreatedByUser)
                .WithMany()
                .HasForeignKey(folder => folder.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FolderPermissionEntity>(entity =>
        {
            entity.ToTable("folder_permissions");
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.FolderId).HasColumnName("folder_id");
            entity.Property(permission => permission.TeamId).HasColumnName("team_id");
            entity.Property(permission => permission.CanView).HasColumnName("can_view");
            entity.Property(permission => permission.CreatedAt).HasColumnName("created_at");
            entity.Property(permission => permission.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(permission => new { permission.FolderId, permission.TeamId }).IsUnique();
            entity.HasOne(permission => permission.Folder)
                .WithMany(folder => folder.TeamPermissions)
                .HasForeignKey(permission => permission.FolderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(permission => permission.Team)
                .WithMany()
                .HasForeignKey(permission => permission.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserFolderPermissionEntity>(entity =>
        {
            entity.ToTable("user_folder_permissions");
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.UserId).HasColumnName("user_id");
            entity.Property(permission => permission.FolderId).HasColumnName("folder_id");
            entity.Property(permission => permission.CanView).HasColumnName("can_view");
            entity.Property(permission => permission.CreatedAt).HasColumnName("created_at");
            entity.Property(permission => permission.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(permission => new { permission.UserId, permission.FolderId }).IsUnique();
            entity.HasOne(permission => permission.User)
                .WithMany()
                .HasForeignKey(permission => permission.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(permission => permission.Folder)
                .WithMany(folder => folder.UserPermissions)
                .HasForeignKey(permission => permission.FolderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentEntity>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.FolderId).HasColumnName("folder_id");
            entity.Property(document => document.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(document => document.Description).HasColumnName("description").HasMaxLength(2000);
            entity.Property(document => document.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(document => document.CurrentVersionId).HasColumnName("current_version_id");
            entity.Property(document => document.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(document => document.CreatedAt).HasColumnName("created_at");
            entity.Property(document => document.UpdatedAt).HasColumnName("updated_at");
            entity.Property(document => document.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(document => new { document.FolderId, document.Title });
            entity.HasOne(document => document.Folder)
                .WithMany()
                .HasForeignKey(document => document.FolderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(document => document.CreatedByUser)
                .WithMany()
                .HasForeignKey(document => document.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentVersionEntity>(entity =>
        {
            entity.ToTable("document_versions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.DocumentId).HasColumnName("document_id");
            entity.Property(version => version.VersionNumber).HasColumnName("version_number");
            entity.Property(version => version.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(500).IsRequired();
            entity.Property(version => version.StoredFilePath).HasColumnName("stored_file_path").HasMaxLength(2000).IsRequired();
            entity.Property(version => version.FileExtension).HasColumnName("file_extension").HasMaxLength(30).IsRequired();
            entity.Property(version => version.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(version => version.ContentType).HasColumnName("content_type").HasMaxLength(200);
            entity.Property(version => version.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(version => version.RejectReason).HasColumnName("reject_reason").HasMaxLength(2000);
            entity.Property(version => version.ExtractedTextPath).HasColumnName("extracted_text_path").HasMaxLength(2000);
            entity.Property(version => version.NormalizedTextPath).HasColumnName("normalized_text_path").HasMaxLength(2000);
            entity.Property(version => version.SectionCount).HasColumnName("section_count");
            entity.Property(version => version.ProcessingWarningsJson).HasColumnName("processing_warnings_json");
            entity.Property(version => version.DocumentSummary).HasColumnName("document_summary").HasMaxLength(2000);
            entity.Property(version => version.Language).HasColumnName("language").HasMaxLength(50);
            entity.Property(version => version.DocumentType).HasColumnName("document_type").HasMaxLength(100);
            entity.Property(version => version.KeyTopicsJson).HasColumnName("key_topics_json");
            entity.Property(version => version.EntitiesJson).HasColumnName("entities_json");
            entity.Property(version => version.EffectiveDate).HasColumnName("effective_date");
            entity.Property(version => version.Sensitivity).HasColumnName("sensitivity").HasMaxLength(50);
            entity.Property(version => version.QualityWarningsJson).HasColumnName("quality_warnings_json");
            entity.Property(version => version.TextHash).HasColumnName("text_hash").HasMaxLength(200);
            entity.Property(version => version.UploadedByUserId).HasColumnName("uploaded_by_user_id");
            entity.Property(version => version.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
            entity.Property(version => version.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(version => version.IndexedAt).HasColumnName("indexed_at");
            entity.Property(version => version.CreatedAt).HasColumnName("created_at");
            entity.Property(version => version.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(version => new { version.DocumentId, version.VersionNumber }).IsUnique();
            entity.HasOne(version => version.Document)
                .WithMany(document => document.Versions)
                .HasForeignKey(version => version.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(version => version.UploadedByUser)
                .WithMany()
                .HasForeignKey(version => version.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(version => version.ReviewedByUser)
                .WithMany()
                .HasForeignKey(version => version.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProcessingJobEntity>(entity =>
        {
            entity.ToTable("processing_jobs");
            entity.HasKey(job => job.Id);
            entity.Property(job => job.JobType).HasColumnName("job_type").HasMaxLength(100).IsRequired();
            entity.Property(job => job.TargetType).HasColumnName("target_type").HasMaxLength(100).IsRequired();
            entity.Property(job => job.TargetId).HasColumnName("target_id");
            entity.Property(job => job.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(job => job.Attempts).HasColumnName("attempts");
            entity.Property(job => job.ErrorMessage).HasColumnName("error_message").HasMaxLength(4000);
            entity.Property(job => job.CreatedAt).HasColumnName("created_at");
            entity.Property(job => job.StartedAt).HasColumnName("started_at");
            entity.Property(job => job.FinishedAt).HasColumnName("finished_at");
            entity.HasIndex(job => new { job.Status, job.CreatedAt });
            entity.HasIndex(job => new { job.TargetType, job.TargetId });
        });

        modelBuilder.Entity<AiInteractionEntity>(entity =>
        {
            entity.ToTable("ai_interactions");
            entity.HasKey(interaction => interaction.Id);
            entity.Property(interaction => interaction.UserId).HasColumnName("user_id");
            entity.Property(interaction => interaction.Question).HasColumnName("question").HasMaxLength(4000).IsRequired();
            entity.Property(interaction => interaction.Answer).HasColumnName("answer").IsRequired();
            entity.Property(interaction => interaction.ScopeType).HasColumnName("scope_type").HasConversion<string>().HasMaxLength(50);
            entity.Property(interaction => interaction.ScopeFolderId).HasColumnName("scope_folder_id");
            entity.Property(interaction => interaction.ScopeDocumentId).HasColumnName("scope_document_id");
            entity.Property(interaction => interaction.NeedsClarification).HasColumnName("needs_clarification");
            entity.Property(interaction => interaction.Confidence).HasColumnName("confidence").HasMaxLength(20).HasDefaultValue("low");
            entity.Property(interaction => interaction.MissingInformationJson).HasColumnName("missing_information_json");
            entity.Property(interaction => interaction.ConflictsJson).HasColumnName("conflicts_json");
            entity.Property(interaction => interaction.SuggestedFollowUpsJson).HasColumnName("suggested_follow_ups_json");
            entity.Property(interaction => interaction.LatencyMs).HasColumnName("latency_ms");
            entity.Property(interaction => interaction.UsedWikiCount).HasColumnName("used_wiki_count");
            entity.Property(interaction => interaction.UsedDocumentCount).HasColumnName("used_document_count");
            entity.Property(interaction => interaction.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(interaction => interaction.CreatedAt);
            entity.HasIndex(interaction => interaction.UserId);
            entity.HasOne(interaction => interaction.User)
                .WithMany()
                .HasForeignKey(interaction => interaction.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiInteractionSourceEntity>(entity =>
        {
            entity.ToTable("ai_interaction_sources");
            entity.HasKey(source => source.Id);
            entity.Property(source => source.AiInteractionId).HasColumnName("ai_interaction_id");
            entity.Property(source => source.SourceType).HasColumnName("source_type").HasConversion<string>().HasMaxLength(50);
            entity.Property(source => source.SourceId).HasColumnName("source_id").HasMaxLength(100).IsRequired();
            entity.Property(source => source.DocumentId).HasColumnName("document_id");
            entity.Property(source => source.DocumentVersionId).HasColumnName("document_version_id");
            entity.Property(source => source.WikiPageId).HasColumnName("wiki_page_id");
            entity.Property(source => source.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(source => source.FolderPath).HasColumnName("folder_path").HasMaxLength(1000).IsRequired();
            entity.Property(source => source.SectionTitle).HasColumnName("section_title").HasMaxLength(300);
            entity.Property(source => source.Excerpt).HasColumnName("excerpt").HasMaxLength(2000).IsRequired();
            entity.Property(source => source.Rank).HasColumnName("rank");
            entity.Property(source => source.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(source => source.AiInteractionId);
            entity.HasOne(source => source.AiInteraction)
                .WithMany(interaction => interaction.Sources)
                .HasForeignKey(source => source.AiInteractionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiFeedbackEntity>(entity =>
        {
            entity.ToTable("ai_feedback");
            entity.HasKey(feedback => feedback.Id);
            entity.Property(feedback => feedback.AiInteractionId).HasColumnName("ai_interaction_id");
            entity.Property(feedback => feedback.UserId).HasColumnName("user_id");
            entity.Property(feedback => feedback.Value).HasColumnName("value").HasConversion<string>().HasMaxLength(50);
            entity.Property(feedback => feedback.Note).HasColumnName("note").HasMaxLength(2000);
            entity.Property(feedback => feedback.ReviewStatus).HasColumnName("review_status").HasConversion<string>().HasMaxLength(50);
            entity.Property(feedback => feedback.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
            entity.Property(feedback => feedback.ReviewerNote).HasColumnName("reviewer_note").HasMaxLength(2000);
            entity.Property(feedback => feedback.CreatedAt).HasColumnName("created_at");
            entity.Property(feedback => feedback.UpdatedAt).HasColumnName("updated_at");
            entity.Property(feedback => feedback.ResolvedAt).HasColumnName("resolved_at");
            entity.HasIndex(feedback => new { feedback.AiInteractionId, feedback.UserId }).IsUnique();
            entity.HasIndex(feedback => new { feedback.Value, feedback.ReviewStatus });
            entity.HasOne(feedback => feedback.AiInteraction)
                .WithMany()
                .HasForeignKey(feedback => feedback.AiInteractionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(feedback => feedback.User)
                .WithMany()
                .HasForeignKey(feedback => feedback.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(feedback => feedback.ReviewedByUser)
                .WithMany()
                .HasForeignKey(feedback => feedback.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiQualityIssueEntity>(entity =>
        {
            entity.ToTable("ai_quality_issues");
            entity.HasKey(issue => issue.Id);
            entity.Property(issue => issue.AiFeedbackId).HasColumnName("ai_feedback_id");
            entity.Property(issue => issue.AiInteractionId).HasColumnName("ai_interaction_id");
            entity.Property(issue => issue.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(issue => issue.FailureType).HasColumnName("failure_type").HasMaxLength(100);
            entity.Property(issue => issue.Severity).HasColumnName("severity").HasMaxLength(50);
            entity.Property(issue => issue.RootCauseHypothesis).HasColumnName("root_cause_hypothesis").HasMaxLength(2000);
            entity.Property(issue => issue.RecommendedActionsJson).HasColumnName("recommended_actions_json");
            entity.Property(issue => issue.EvidenceJson).HasColumnName("evidence_json");
            entity.Property(issue => issue.CreatedAt).HasColumnName("created_at");
            entity.Property(issue => issue.UpdatedAt).HasColumnName("updated_at");
            entity.Property(issue => issue.ClassifiedAt).HasColumnName("classified_at");
            entity.Property(issue => issue.ResolvedAt).HasColumnName("resolved_at");
            entity.HasIndex(issue => issue.AiFeedbackId).IsUnique();
            entity.HasIndex(issue => new { issue.Status, issue.CreatedAt });
            entity.HasOne(issue => issue.AiFeedback)
                .WithMany()
                .HasForeignKey(issue => issue.AiFeedbackId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(issue => issue.AiInteraction)
                .WithMany()
                .HasForeignKey(issue => issue.AiInteractionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeCorrectionEntity>(entity =>
        {
            entity.ToTable("knowledge_corrections");
            entity.HasKey(correction => correction.Id);
            entity.Property(correction => correction.QualityIssueId).HasColumnName("quality_issue_id");
            entity.Property(correction => correction.AiFeedbackId).HasColumnName("ai_feedback_id");
            entity.Property(correction => correction.AiInteractionId).HasColumnName("ai_interaction_id");
            entity.Property(correction => correction.Question).HasColumnName("question").HasMaxLength(4000).IsRequired();
            entity.Property(correction => correction.CorrectionText).HasColumnName("correction_text").IsRequired();
            entity.Property(correction => correction.VisibilityScope).HasColumnName("visibility_scope").HasConversion<string>().HasMaxLength(50);
            entity.Property(correction => correction.FolderId).HasColumnName("folder_id");
            entity.Property(correction => correction.DocumentId).HasColumnName("document_id");
            entity.Property(correction => correction.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(correction => correction.RejectReason).HasColumnName("reject_reason").HasMaxLength(2000);
            entity.Property(correction => correction.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(correction => correction.ApprovedByUserId).HasColumnName("approved_by_user_id");
            entity.Property(correction => correction.CreatedAt).HasColumnName("created_at");
            entity.Property(correction => correction.UpdatedAt).HasColumnName("updated_at");
            entity.Property(correction => correction.ApprovedAt).HasColumnName("approved_at");
            entity.Property(correction => correction.IndexedAt).HasColumnName("indexed_at");
            entity.HasIndex(correction => correction.QualityIssueId);
            entity.HasIndex(correction => new { correction.Status, correction.CreatedAt });
            entity.HasOne(correction => correction.QualityIssue)
                .WithMany()
                .HasForeignKey(correction => correction.QualityIssueId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(correction => correction.AiFeedback)
                .WithMany()
                .HasForeignKey(correction => correction.AiFeedbackId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(correction => correction.AiInteraction)
                .WithMany()
                .HasForeignKey(correction => correction.AiInteractionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(correction => correction.Folder)
                .WithMany()
                .HasForeignKey(correction => correction.FolderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(correction => correction.Document)
                .WithMany()
                .HasForeignKey(correction => correction.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(correction => correction.CreatedByUser)
                .WithMany()
                .HasForeignKey(correction => correction.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(correction => correction.ApprovedByUser)
                .WithMany()
                .HasForeignKey(correction => correction.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RetrievalHintEntity>(entity =>
        {
            entity.ToTable("retrieval_hints");
            entity.HasKey(hint => hint.Id);
            entity.Property(hint => hint.CorrectionId).HasColumnName("correction_id");
            entity.Property(hint => hint.HintText).HasColumnName("hint_text").HasMaxLength(1000).IsRequired();
            entity.Property(hint => hint.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(hint => hint.CorrectionId);
            entity.HasOne(hint => hint.Correction)
                .WithMany()
                .HasForeignKey(hint => hint.CorrectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeChunkIndexEntity>(entity =>
        {
            entity.ToTable("knowledge_chunk_indexes");
            entity.HasKey(chunk => chunk.ChunkId);
            entity.Property(chunk => chunk.ChunkId).HasColumnName("chunk_id").HasMaxLength(200);
            entity.Property(chunk => chunk.SourceType).HasColumnName("source_type").HasConversion<string>().HasMaxLength(50);
            entity.Property(chunk => chunk.SourceId).HasColumnName("source_id").HasMaxLength(100).IsRequired();
            entity.Property(chunk => chunk.DocumentId).HasColumnName("document_id");
            entity.Property(chunk => chunk.DocumentVersionId).HasColumnName("document_version_id");
            entity.Property(chunk => chunk.WikiPageId).HasColumnName("wiki_page_id");
            entity.Property(chunk => chunk.FolderId).HasColumnName("folder_id");
            entity.Property(chunk => chunk.VisibilityScope).HasColumnName("visibility_scope").HasMaxLength(50).IsRequired();
            entity.Property(chunk => chunk.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(chunk => chunk.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(chunk => chunk.FolderPath).HasColumnName("folder_path").HasMaxLength(1000).IsRequired();
            entity.Property(chunk => chunk.SectionTitle).HasColumnName("section_title").HasMaxLength(300);
            entity.Property(chunk => chunk.SectionIndex).HasColumnName("section_index");
            entity.Property(chunk => chunk.Text).HasColumnName("text").IsRequired();
            entity.Property(chunk => chunk.NormalizedText).HasColumnName("normalized_text").IsRequired();
            entity.Property(chunk => chunk.CreatedAt).HasColumnName("created_at");
            entity.Property(chunk => chunk.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(chunk => new { chunk.SourceType, chunk.SourceId });
            entity.HasIndex(chunk => new { chunk.SourceType, chunk.Status });
            entity.HasIndex(chunk => chunk.FolderId);
            entity.HasIndex(chunk => chunk.DocumentId);
        });

        modelBuilder.Entity<EvaluationCaseEntity>(entity =>
        {
            entity.ToTable("evaluation_cases");
            entity.HasKey(evaluationCase => evaluationCase.Id);
            entity.Property(evaluationCase => evaluationCase.SourceFeedbackId).HasColumnName("source_feedback_id");
            entity.Property(evaluationCase => evaluationCase.Question).HasColumnName("question").HasMaxLength(4000).IsRequired();
            entity.Property(evaluationCase => evaluationCase.ExpectedAnswer).HasColumnName("expected_answer").IsRequired();
            entity.Property(evaluationCase => evaluationCase.ExpectedKeywordsJson).HasColumnName("expected_keywords_json");
            entity.Property(evaluationCase => evaluationCase.ScopeType).HasColumnName("scope_type").HasConversion<string>().HasMaxLength(50);
            entity.Property(evaluationCase => evaluationCase.FolderId).HasColumnName("folder_id");
            entity.Property(evaluationCase => evaluationCase.DocumentId).HasColumnName("document_id");
            entity.Property(evaluationCase => evaluationCase.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(evaluationCase => evaluationCase.IsActive).HasColumnName("is_active");
            entity.Property(evaluationCase => evaluationCase.CreatedAt).HasColumnName("created_at");
            entity.Property(evaluationCase => evaluationCase.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(evaluationCase => new { evaluationCase.IsActive, evaluationCase.CreatedAt });
            entity.HasIndex(evaluationCase => evaluationCase.SourceFeedbackId);
            entity.HasOne(evaluationCase => evaluationCase.SourceFeedback)
                .WithMany()
                .HasForeignKey(evaluationCase => evaluationCase.SourceFeedbackId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(evaluationCase => evaluationCase.Folder)
                .WithMany()
                .HasForeignKey(evaluationCase => evaluationCase.FolderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(evaluationCase => evaluationCase.Document)
                .WithMany()
                .HasForeignKey(evaluationCase => evaluationCase.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(evaluationCase => evaluationCase.CreatedByUser)
                .WithMany()
                .HasForeignKey(evaluationCase => evaluationCase.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EvaluationRunEntity>(entity =>
        {
            entity.ToTable("evaluation_runs");
            entity.HasKey(run => run.Id);
            entity.Property(run => run.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(run => run.TotalCases).HasColumnName("total_cases");
            entity.Property(run => run.PassedCases).HasColumnName("passed_cases");
            entity.Property(run => run.FailedCases).HasColumnName("failed_cases");
            entity.Property(run => run.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(run => run.CreatedAt).HasColumnName("created_at");
            entity.Property(run => run.FinishedAt).HasColumnName("finished_at");
            entity.HasIndex(run => run.CreatedAt);
            entity.HasOne(run => run.CreatedByUser)
                .WithMany()
                .HasForeignKey(run => run.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EvaluationRunResultEntity>(entity =>
        {
            entity.ToTable("evaluation_run_results");
            entity.HasKey(result => result.Id);
            entity.Property(result => result.EvaluationRunId).HasColumnName("evaluation_run_id");
            entity.Property(result => result.EvaluationCaseId).HasColumnName("evaluation_case_id");
            entity.Property(result => result.AiInteractionId).HasColumnName("ai_interaction_id");
            entity.Property(result => result.ActualAnswer).HasColumnName("actual_answer").IsRequired();
            entity.Property(result => result.Passed).HasColumnName("passed");
            entity.Property(result => result.Score).HasColumnName("score");
            entity.Property(result => result.FailureReason).HasColumnName("failure_reason").HasMaxLength(2000);
            entity.Property(result => result.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(result => result.EvaluationRunId);
            entity.HasIndex(result => new { result.EvaluationCaseId, result.CreatedAt });
            entity.HasOne(result => result.EvaluationRun)
                .WithMany(run => run.Results)
                .HasForeignKey(result => result.EvaluationRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(result => result.EvaluationCase)
                .WithMany()
                .HasForeignKey(result => result.EvaluationCaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(result => result.AiInteraction)
                .WithMany()
                .HasForeignKey(result => result.AiInteractionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WikiDraftEntity>(entity =>
        {
            entity.ToTable("wiki_drafts");
            entity.HasKey(draft => draft.Id);
            entity.Property(draft => draft.SourceDocumentId).HasColumnName("source_document_id");
            entity.Property(draft => draft.SourceDocumentVersionId).HasColumnName("source_document_version_id");
            entity.Property(draft => draft.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(draft => draft.Content).HasColumnName("content").IsRequired();
            entity.Property(draft => draft.Language).HasColumnName("language").HasMaxLength(50).IsRequired();
            entity.Property(draft => draft.MissingInformationJson).HasColumnName("missing_information_json");
            entity.Property(draft => draft.RelatedDocumentsJson).HasColumnName("related_documents_json");
            entity.Property(draft => draft.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(draft => draft.RejectReason).HasColumnName("reject_reason").HasMaxLength(2000);
            entity.Property(draft => draft.GeneratedByUserId).HasColumnName("generated_by_user_id");
            entity.Property(draft => draft.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
            entity.Property(draft => draft.CreatedAt).HasColumnName("created_at");
            entity.Property(draft => draft.UpdatedAt).HasColumnName("updated_at");
            entity.Property(draft => draft.ReviewedAt).HasColumnName("reviewed_at");
            entity.HasIndex(draft => draft.Status);
            entity.HasIndex(draft => draft.SourceDocumentId);
            entity.HasOne(draft => draft.SourceDocument)
                .WithMany()
                .HasForeignKey(draft => draft.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(draft => draft.SourceDocumentVersion)
                .WithMany()
                .HasForeignKey(draft => draft.SourceDocumentVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(draft => draft.GeneratedByUser)
                .WithMany()
                .HasForeignKey(draft => draft.GeneratedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(draft => draft.ReviewedByUser)
                .WithMany()
                .HasForeignKey(draft => draft.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WikiPageEntity>(entity =>
        {
            entity.ToTable("wiki_pages");
            entity.HasKey(page => page.Id);
            entity.Property(page => page.SourceDraftId).HasColumnName("source_draft_id");
            entity.Property(page => page.SourceDocumentId).HasColumnName("source_document_id");
            entity.Property(page => page.SourceDocumentVersionId).HasColumnName("source_document_version_id");
            entity.Property(page => page.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(page => page.Content).HasColumnName("content").IsRequired();
            entity.Property(page => page.Language).HasColumnName("language").HasMaxLength(50).IsRequired();
            entity.Property(page => page.VisibilityScope).HasColumnName("visibility_scope").HasConversion<string>().HasMaxLength(50);
            entity.Property(page => page.FolderId).HasColumnName("folder_id");
            entity.Property(page => page.IsCompanyPublicConfirmed).HasColumnName("is_company_public_confirmed");
            entity.Property(page => page.PublishedByUserId).HasColumnName("published_by_user_id");
            entity.Property(page => page.PublishedAt).HasColumnName("published_at");
            entity.Property(page => page.ArchivedAt).HasColumnName("archived_at");
            entity.Property(page => page.CreatedAt).HasColumnName("created_at");
            entity.Property(page => page.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(page => page.SourceDraftId).IsUnique();
            entity.HasIndex(page => page.VisibilityScope);
            entity.HasOne(page => page.SourceDraft)
                .WithMany()
                .HasForeignKey(page => page.SourceDraftId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(page => page.SourceDocument)
                .WithMany()
                .HasForeignKey(page => page.SourceDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(page => page.SourceDocumentVersion)
                .WithMany()
                .HasForeignKey(page => page.SourceDocumentVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(page => page.Folder)
                .WithMany()
                .HasForeignKey(page => page.FolderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(page => page.PublishedByUser)
                .WithMany()
                .HasForeignKey(page => page.PublishedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(log => log.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            entity.Property(log => log.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            entity.Property(log => log.EntityId).HasColumnName("entity_id");
            entity.Property(log => log.MetadataJson).HasColumnName("metadata_json");
            entity.Property(log => log.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(log => log.CreatedAt);
            entity.HasIndex(log => log.Action);
            entity.HasIndex(log => log.EntityType);
            entity.HasOne(log => log.ActorUser)
                .WithMany()
                .HasForeignKey(log => log.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
