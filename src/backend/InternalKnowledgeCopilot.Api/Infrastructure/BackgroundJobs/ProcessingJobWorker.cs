using InternalKnowledgeCopilot.Api.Infrastructure.Options;
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
        var processingJobService = scope.ServiceProvider.GetRequiredService<IProcessingJobService>();
        await processingJobService.ProcessNextJobAsync(cancellationToken);
    }
}
