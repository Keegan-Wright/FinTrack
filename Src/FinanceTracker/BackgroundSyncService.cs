using System.Threading.Channels;

namespace FinanceTracker;

public interface IBackgroundSyncService
{
    ValueTask NotifySyncComplete();
    IAsyncEnumerable<bool> GetSyncNotifications(CancellationToken cancellationToken);
}

public class BackgroundSyncService : IBackgroundSyncService
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(10)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public async ValueTask NotifySyncComplete()
    {
        await _channel.Writer.WriteAsync(true);
    }

    public IAsyncEnumerable<bool> GetSyncNotifications(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
