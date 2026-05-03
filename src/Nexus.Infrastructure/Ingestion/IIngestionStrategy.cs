using Nexus.Application.DTOs;

namespace Nexus.Infrastructure.Ingestion;

public interface IIngestionStrategy
{
    bool CanHandle(string fileExtension);
    IAsyncEnumerable<RawCarrierRecord> ReadAsync(string filePath, string carrierCode, CancellationToken cancellationToken);
}
