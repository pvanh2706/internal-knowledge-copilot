using InternalKnowledgeCopilot.Api.Common;
using InternalKnowledgeCopilot.Api.Infrastructure.Database;
using InternalKnowledgeCopilot.Api.Infrastructure.DocumentProcessing;
using InternalKnowledgeCopilot.Api.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InternalKnowledgeCopilot.Api.Infrastructure.BackgroundJobs;

public sealed class ProcessingJobWorker(IServiceScopeFactory scopeFactory, IOptions<BackgroundJobOptions> options, ILogger<ProcessingJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Processing job worker failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollSeconds)), stoppingToken);
        }
    }

    private async Task ProcessNextJobAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processingService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
        var maxAttempts = Math.Max(1, options.Value.MaxAttempts);

        var job = await dbContext.ProcessingJobs
            .Where(item => item.Status == ProcessingJobStatus.Pending && item.Attempts < maxAttempts)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return;
        }

        job.Status = ProcessingJobStatus.Running;
        job.Attempts += 1;
        job.StartedAt = DateTimeOffset.UtcNow;
        job.FinishedAt = null;
        job.ErrorMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            if (job.JobType == "ExtractAndEmbedDocument" && job.TargetType == "DocumentVersion")
            {
                await processingService.ProcessDocumentVersionAsync(job.TargetId, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported processing job: {job.JobType}/{job.TargetType}");
            }

            job.Status = ProcessingJobStatus.Succeeded;
            job.ErrorMessage = null;
            job.FinishedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            var hasAttemptsRemaining = job.Attempts < maxAttempts;
            job.Status = hasAttemptsRemaining ? ProcessingJobStatus.Pending : ProcessingJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.FinishedAt = hasAttemptsRemaining ? null : DateTimeOffset.UtcNow;
            logger.LogWarning(ex, "Processing job {JobId} failed on attempt {Attempt}/{MaxAttempts}.", job.Id, job.Attempts, maxAttempts);

            if (!hasAttemptsRemaining)
            {
                var version = await dbContext.DocumentVersions.FirstOrDefaultAsync(item => item.Id == job.TargetId, cancellationToken);
                if (version is not null)
                {
                    version.Status = DocumentVersionStatus.ProcessingFailed;
                    version.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
