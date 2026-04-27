namespace Nexus.API.Background;

public interface IIngestionQueue
{
    ValueTask QueueAsync(IngestionJob job, CancellationToken cancellationToken);
    ValueTask<IngestionJob> DequeueAsync(CancellationToken cancellationToken);
}
