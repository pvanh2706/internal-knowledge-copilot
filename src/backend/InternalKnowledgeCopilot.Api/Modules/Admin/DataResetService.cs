using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Modules.Admin;

public interface IDataResetService
{
    bool IsEnabled { get; }

    string ConfirmationPhrase { get; }

    Task<DataResetResponse> ResetAsync(Guid adminUserId, DataResetRequest request, CancellationToken cancellationToken = default);
}

public sealed class DataResetService(
    AppDbContext dbContext,
    IKnowledgeVectorStore vectorStore,
    IAuditLogService auditLogService,
    IOptions<DataResetOptions> dataResetOptions,
    IOptions<AppStorageOptions> storageOptions,
    IWebHostEnvironment environment) : IDataResetService
{
    private static readonly string[] ResetTableNames =
    [
        "evaluation_run_results",
        "evaluation_runs",
        "evaluation_cases",
        "retrieval_hints",
        "knowledge_corrections",
        "ai_quality_issues",
        "ai_feedback",
        "ai_interaction_sources",
        "ai_interactions",
        "wiki_pages",
        "wiki_drafts",
        "knowledge_chunk_indexes",
        "knowledge_chunks",
        "processing_jobs",
        "document_versions",
        "documents",
        "folder_permissions",
        "user_folder_permissions",
        "folders",
        "audit_logs",
    ];

    public bool IsEnabled => dataResetOptions.Value.Enabled || environment.IsDevelopment();

    public string ConfirmationPhrase => string.IsNullOrWhiteSpace(dataResetOptions.Value.ConfirmationPhrase)
        ? "RESET TEST DATA"
        : dataResetOptions.Value.ConfirmationPhrase;

    public async Task<DataResetResponse> ResetAsync(Guid adminUserId, DataResetRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("data_reset_disabled");
        }

        if (!string.Equals(request.ConfirmationPhrase?.Trim(), ConfirmationPhrase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("invalid_confirmation_phrase");
        }

        var databaseRowsDeleted = await ResetDatabaseAsync(cancellationToken);
        var storageItemsDeleted = request.ResetStorage
            ? ResetStorage()
            : 0;
        var vectorStoreReset = false;
        if (request.ResetVectorStore)
        {
            await vectorStore.ResetCollectionAsync(cancellationToken);
            vectorStoreReset = true;
        }

        var completedAt = DateTimeOffset.UtcNow;
        await auditLogService.RecordAsync(
            adminUserId,
            "ApplicationDataReset",
            "System",
            null,
            new
            {
                DatabaseRowsDeleted = databaseRowsDeleted,
                StorageItemsDeleted = storageItemsDeleted,
                VectorStoreReset = vectorStoreReset,
                UsersTeamsAndAiSettingsPreserved = true,
            },
            cancellationToken);

        return new DataResetResponse(
            completedAt,
            databaseRowsDeleted,
            storageItemsDeleted,
            vectorStoreReset,
            UsersTeamsAndAiSettingsPreserved: true);
    }

    private async Task<int> ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;", cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        dbContext.Database.UseTransaction(transaction);
        try
        {
            var deletedRows = 0;
            foreach (var tableName in ResetTableNames)
            {
                var sql = string.Concat("DELETE FROM ", tableName, ";");
                var affectedRows = await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                if (affectedRows > 0)
                {
                    deletedRows += affectedRows;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return deletedRows;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            dbContext.Database.UseTransaction(null);
            await dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private int ResetStorage()
    {
        var rootPath = Path.GetFullPath(storageOptions.Value.RootPath);
        EnsureSafeStorageRoot(rootPath);

        if (!Directory.Exists(rootPath))
        {
            Directory.CreateDirectory(rootPath);
            return 0;
        }

        var fileCount = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories).Count();
        var directoryCount = Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories).Count();

        foreach (var file in Directory.EnumerateFiles(rootPath))
        {
            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(rootPath))
        {
            Directory.Delete(directory, recursive: true);
        }

        return fileCount + directoryCount;
    }

    private static void EnsureSafeStorageRoot(string rootPath)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(rootPath);
        var driveRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullRoot) ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fullRoot) || string.Equals(fullRoot, driveRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("unsafe_storage_root");
        }
    }
}
