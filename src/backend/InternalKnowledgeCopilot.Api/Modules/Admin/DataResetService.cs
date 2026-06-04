using InternalKnowledgeCopilot.Api.Infrastructure.Audit;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using InternalKnowledgeCopilot.Api.Infrastructure.Tenancy;
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
    ITenantContext tenantContext,
    IOptions<DataResetOptions> dataResetOptions,
    IOptions<AppStorageOptions> storageOptions,
    IWebHostEnvironment environment) : IDataResetService
{
    private static readonly string[] ResetTableNames =
    [
        "evaluation_run_results",
        "evaluation_runs",
        "evaluation_cases",
        "external_acl_snapshots",
        "external_objects",
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

        var tenantId = tenantContext.GetRequiredTenantId();
        var tenantStorageTargets = request.ResetStorage
            ? await GetTenantStorageTargetsAsync(tenantId, cancellationToken)
            : [];
        var databaseRowsDeleted = await ResetDatabaseAsync(tenantId, cancellationToken);
        var storageItemsDeleted = request.ResetStorage
            ? ResetStorage(tenantStorageTargets)
            : 0;
        var vectorStoreReset = false;
        if (request.ResetVectorStore)
        {
            await vectorStore.DeleteTenantDataAsync(tenantId, cancellationToken);
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
                TenantId = tenantId,
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

    private async Task<int> ResetDatabaseAsync(Guid tenantId, CancellationToken cancellationToken)
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
                var sql = $"DELETE FROM {tableName} WHERE tenant_id = '{tenantId:D}';";
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

    private async Task<IReadOnlyList<string>> GetTenantStorageTargetsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.DocumentVersions
            .AsNoTracking()
            .Where(version => version.TenantId == tenantId)
            .Select(version => new
            {
                version.StoredFilePath,
                version.ExtractedTextPath,
                version.NormalizedTextPath,
            })
            .ToListAsync(cancellationToken);

        return rows
            .SelectMany(row => new[] { row.StoredFilePath, row.ExtractedTextPath, row.NormalizedTextPath })
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int ResetStorage(IReadOnlyList<string> tenantStorageTargets)
    {
        var rootPath = Path.GetFullPath(storageOptions.Value.RootPath);
        EnsureSafeStorageRoot(rootPath);

        if (!Directory.Exists(rootPath))
        {
            Directory.CreateDirectory(rootPath);
            return 0;
        }

        var targetDirectories = tenantStorageTargets
            .Select(path => TryResolveUnderRoot(rootPath, path, out var resolvedPath) ? Path.GetDirectoryName(resolvedPath) : null)
            .Where(directory => !string.IsNullOrWhiteSpace(directory) && IsSafeTenantStorageDirectory(rootPath, directory))
            .Select(directory => Path.GetFullPath(directory!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(directory => directory.Length)
            .ToList();

        var deletedItems = 0;
        foreach (var directory in targetDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            deletedItems += Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count();
            deletedItems += Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories).Count() + 1;
            Directory.Delete(directory, recursive: true);
        }

        deletedItems += DeleteEmptyParentDirectories(rootPath, targetDirectories);
        return deletedItems;
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

    private static bool TryResolveUnderRoot(string rootPath, string storedPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return false;
        }

        var rootWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
        var candidatePath = Path.GetFullPath(storedPath);
        if (!candidatePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        resolvedPath = candidatePath;
        return true;
    }

    private static bool IsSafeTenantStorageDirectory(string rootPath, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var fullDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        return !string.Equals(fullRoot, fullDirectory, StringComparison.OrdinalIgnoreCase)
            && fullDirectory.StartsWith(EnsureTrailingSeparator(fullRoot), StringComparison.OrdinalIgnoreCase);
    }

    private static int DeleteEmptyParentDirectories(string rootPath, IReadOnlyList<string> targetDirectories)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var deletedDirectories = 0;
        var parentDirectories = targetDirectories
            .Select(Path.GetDirectoryName)
            .Where(directory => IsSafeTenantStorageDirectory(root, directory))
            .Select(directory => Path.GetFullPath(directory!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(directory => directory.Length);

        foreach (var directory in parentDirectories)
        {
            if (!Directory.Exists(directory) || Directory.EnumerateFileSystemEntries(directory).Any())
            {
                continue;
            }

            Directory.Delete(directory, recursive: false);
            deletedDirectories += 1;
        }

        return deletedDirectories;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
