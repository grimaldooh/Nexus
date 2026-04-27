using System.Threading.Channels;

namespace Nexus.API.Background;

public class IngestionQueue : IIngestionQueue
{
    private readonly Channel<IngestionJob> _channel = Channel.CreateUnbounded<IngestionJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask QueueAsync(IngestionJob job, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public ValueTask<IngestionJob> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
