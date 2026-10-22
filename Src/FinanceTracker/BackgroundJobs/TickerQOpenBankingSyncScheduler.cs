using FinanceTracker.Infrastructure.OpenBanking;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Queues <see cref="SyncUserOpenBankingDetailsAsync"/> through TickerQ, which Infrastructure can't name because the
/// jobs live here.
/// </summary>
public sealed class TickerQOpenBankingSyncScheduler : IOpenBankingSyncScheduler
{
    private readonly ITimeTickerManager<TimeTickerEntity> _timeTickerManager;
    private readonly TimeProvider _timeProvider;

    public TickerQOpenBankingSyncScheduler(ITimeTickerManager<TimeTickerEntity> timeTickerManager,
        TimeProvider timeProvider)
    {
        _timeTickerManager = timeTickerManager;
        _timeProvider = timeProvider;
    }

    public Task ScheduleUserSyncAsync(Guid userId, CancellationToken cancellationToken) =>
        _timeTickerManager.AddAsync<SyncUserOpenBankingDetailsAsync, SyncUserOpenBankingDetailsRequest>(
            _timeProvider.GetUtcNow().UtcDateTime, new SyncUserOpenBankingDetailsRequest(userId), cancellationToken);
}
