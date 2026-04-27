namespace Nexus.Application.Interfaces;

public interface ISanitizationService
{
    Task RunAsync(Guid batchId, CancellationToken cancellationToken);
}
