using Microsoft.Extensions.Logging;
using Nexus.Application.DTOs;

namespace Nexus.Infrastructure.Ingestion;

public class ExcelIngestionStrategy : IIngestionStrategy
{
    private readonly ILogger<ExcelIngestionStrategy> _logger;

    public ExcelIngestionStrategy(ILogger<ExcelIngestionStrategy> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(string fileExtension)
    {
        return string.Equals(fileExtension, ".xlsx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileExtension, ".xls", StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<RawCarrierRecord> ReadAsync(
        string filePath,
        string carrierCode,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        _logger.LogWarning("Excel ingestion strategy is a placeholder. File {FilePath} skipped.", filePath);
        yield break;
    }
}
