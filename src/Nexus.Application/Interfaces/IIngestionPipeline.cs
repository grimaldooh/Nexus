namespace Nexus.Application.Interfaces;

public interface IIngestionPipeline
{
    Task StageAsync(Guid batchId, string filePath, string carrierCode, CancellationToken cancellationToken);
}
