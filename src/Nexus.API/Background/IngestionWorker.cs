using Microsoft.EntityFrameworkCore;
using Nexus.Application.Interfaces;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Data;

namespace Nexus.API.Background;

public class IngestionWorker : BackgroundService
{
    private readonly IIngestionQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IngestionWorker> _logger;

    public IngestionWorker(IIngestionQueue queue, IServiceProvider serviceProvider, ILogger<IngestionWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(stoppingToken);
            await ProcessJobAsync(job, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(IngestionJob job, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var sanitizationService = scope.ServiceProvider.GetRequiredService<ISanitizationService>();
        var ingestionPipeline = scope.ServiceProvider.GetRequiredService<IIngestionPipeline>();

        var batch = await dbContext.Batches.FirstOrDefaultAsync(x => x.Id == job.BatchId, cancellationToken);
        if (batch is null)
        {
            _logger.LogWarning("Batch {BatchId} not found for ingestion.", job.BatchId);
            return;
        }

        try
        {
            batch.Status = BatchStatus.Processing;
            await dbContext.SaveChangesAsync(cancellationToken);

            await ingestionPipeline.StageAsync(batch.Id, job.FilePath, job.CarrierCode, cancellationToken);

            await sanitizationService.RunAsync(batch.Id, cancellationToken);

            batch.Status = BatchStatus.Completed;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process batch {BatchId}.", batch.Id);
            batch.Status = BatchStatus.Failed;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            if (File.Exists(job.FilePath))
            {
                File.Delete(job.FilePath);
            }
        }
    }

}
