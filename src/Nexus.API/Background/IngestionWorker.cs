using System.Globalization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.Interfaces;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Data;

namespace Nexus.API.Background;

public class IngestionWorker : BackgroundService
{
    private const int BatchSize = 500;
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

            using var stream = File.OpenRead(job.FilePath);
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            var buffer = new List<InsuranceTransaction>(BatchSize);
            foreach (var record in csv.GetRecords<InsuranceTransactionCsvRow>())
            {
                var transaction = new InsuranceTransaction
                {
                    Id = Guid.NewGuid(),
                    BatchId = batch.Id,
                    ExternalId = record.ExternalId,
                    PolicyNumber = record.PolicyNumber,
                    GrossPremium = record.GrossPremium,
                    NetCommission = record.NetCommission,
                    CarrierCode = record.CarrierCode,
                    TransactionDate = record.TransactionDate,
                    Notes = record.Notes,
                    Status = TransactionStatus.Pending
                };

                buffer.Add(transaction);

                if (buffer.Count >= BatchSize)
                {
                    dbContext.InsuranceTransactions.AddRange(buffer);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
            {
                dbContext.InsuranceTransactions.AddRange(buffer);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

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

    private sealed class InsuranceTransactionCsvRow
    {
        public string ExternalId { get; set; } = string.Empty;
        public string PolicyNumber { get; set; } = string.Empty;
        public decimal NetCommission { get; set; }
        public decimal? GrossPremium { get; set; }
        public string CarrierCode { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string? Notes { get; set; }
    }
}
