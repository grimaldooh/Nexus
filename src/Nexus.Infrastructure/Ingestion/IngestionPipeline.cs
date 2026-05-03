using Microsoft.Extensions.Logging;
using Nexus.Application.Interfaces;
using Nexus.Domain.Entities;
using Nexus.Domain.Interfaces;
using Nexus.Infrastructure.Data;

namespace Nexus.Infrastructure.Ingestion;

public class IngestionPipeline : IIngestionPipeline
{
    private const int BatchSize = 1000;
    private readonly IEnumerable<IIngestionStrategy> _strategies;
    private readonly ICarrierMappingService _mappingService;
    private readonly IPiiMaskingService _piiMaskingService;
    private readonly NexusDbContext _dbContext;
    private readonly ILogger<IngestionPipeline> _logger;

    public IngestionPipeline(
        IEnumerable<IIngestionStrategy> strategies,
        ICarrierMappingService mappingService,
        IPiiMaskingService piiMaskingService,
        NexusDbContext dbContext,
        ILogger<IngestionPipeline> logger)
    {
        _strategies = strategies;
        _mappingService = mappingService;
        _piiMaskingService = piiMaskingService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task StageAsync(Guid batchId, string filePath, string carrierCode, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);
        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(extension));
        if (strategy is null)
        {
            throw new InvalidOperationException($"No ingestion strategy registered for {extension}.");
        }

        var buffer = new List<InsuranceTransaction>(BatchSize);

        await foreach (var record in strategy.ReadAsync(filePath, carrierCode, cancellationToken))
        {
            var transaction = await _mappingService.MapAsync(batchId, record, cancellationToken);
            _piiMaskingService.Mask(transaction);

            buffer.Add(transaction);

            if (buffer.Count >= BatchSize)
            {
                await FlushAsync(buffer, cancellationToken);
            }
        }

        if (buffer.Count > 0)
        {
            await FlushAsync(buffer, cancellationToken);
        }

        _logger.LogInformation("Staged records for batch {BatchId} from file {FilePath}.", batchId, filePath);
    }

    private async Task FlushAsync(List<InsuranceTransaction> buffer, CancellationToken cancellationToken)
    {
        _dbContext.InsuranceTransactions.AddRange(buffer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        buffer.Clear();
    }
}
