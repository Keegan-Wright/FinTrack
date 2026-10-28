using FinanceTracker.Contracts;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.OpenBanking.Balances;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public abstract class OpenBankingSyncJobTestBase : InfrastructureTestBase
{
    protected const string Username = "primary";
    protected const string OtherUsername = "secondary";

    /// <summary>
    /// When the jobs run. Midday, so the local day is the same as the UTC one wherever the tests run.
    /// </summary>
    protected static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    protected static readonly DateOnly Today = new(2026, 10, 28);

    private ServiceProvider _services = null!;

    /// <summary>
    /// The user of the scope <see cref="OpenBankingService"/> was last resolved from.
    /// </summary>
    private ICurrentUser? _resolvedFor;

    /// <summary>
    /// The user of the scope <see cref="TransferMatchingService"/> was last resolved from.
    /// </summary>
    private ICurrentUser? _matchingFor;

    /// <summary>
    /// The user of the scope <see cref="BalanceSnapshotService"/> was last resolved from.
    /// </summary>
    private ICurrentUser? _snapshotFor;

    protected IOpenBankingSyncService OpenBankingService { get; } = Substitute.For<IOpenBankingSyncService>();
    protected ISyncNotifier SyncNotifier { get; } = Substitute.For<ISyncNotifier>();
    protected ITransferMatchingService TransferMatchingService { get; } = Substitute.For<ITransferMatchingService>();
    protected IBalanceSnapshotService BalanceSnapshotService { get; } = Substitute.For<IBalanceSnapshotService>();
    protected TimeProvider Clock { get; } = new FixedTimeProvider(Now);

    /// <summary>
    /// Scopes the jobs resolve <see cref="OpenBankingService"/> from, with a real <see cref="CurrentUser"/>.
    /// </summary>
    protected IServiceScopeFactory ScopeFactory => _services.GetRequiredService<IServiceScopeFactory>();

    /// <summary>
    /// Each connection synced, with the user the service's scope was acting as at the time.
    /// </summary>
    protected List<(Guid UserId, Guid ConnectionId)> Synced { get; } = [];

    /// <summary>
    /// Each user whose transfers were matched, with how many connections had synced by then.
    /// </summary>
    protected List<(Guid UserId, int SyncedBefore)> Matched { get; } = [];

    /// <summary>
    /// Each user whose balances were snapshot, with the day, when the sync started, how many connections had synced
    /// by then and whether the user's transfers had already been matched.
    /// </summary>
    protected List<(Guid UserId, DateOnly Day, DateTime SyncedFrom, int SyncedBefore, bool AlreadyMatched)> Snapshots
    {
        get;
    } = [];

    [Before(Test)]
    public void SetupOpenBankingService()
    {
        _services = new ServiceCollection()
            .AddCurrentUser()
            .AddScoped(sp =>
            {
                _resolvedFor = sp.GetRequiredService<ICurrentUser>();
                return OpenBankingService;
            })
            .AddScoped(sp =>
            {
                _matchingFor = sp.GetRequiredService<ICurrentUser>();
                return TransferMatchingService;
            })
            .AddScoped(sp =>
            {
                _snapshotFor = sp.GetRequiredService<ICurrentUser>();
                return BalanceSnapshotService;
            })
            .BuildServiceProvider();

        BalanceSnapshotService
            .When(x => x.SnapshotAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()))
            .Do(callInfo =>
            {
                if (_snapshotFor is not null)
                {
                    Guid userId = _snapshotFor.UserId;
                    bool alreadyMatched = Matched.Any(x => x.UserId == userId);
                    Snapshots.Add((userId, callInfo.ArgAt<DateOnly>(0), callInfo.ArgAt<DateTime>(1), Synced.Count,
                        alreadyMatched));
                }
            });

        TransferMatchingService
            .When(x => x.MatchChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                if (_matchingFor is not null)
                {
                    Matched.Add((_matchingFor.UserId, Synced.Count));
                }
            });

        OpenBankingService
            .When(x => x.SyncConnectionAsync(Arg.Any<Guid>(), Arg.Any<SyncTypes>(), Arg.Any<CancellationToken>()))
            .Do(callInfo =>
            {
                // Arranging a substitute's behaviour calls it too, only record calls the job makes through a scope
                if (_resolvedFor is not null)
                {
                    Synced.Add((_resolvedFor.UserId, callInfo.Arg<Guid>()));
                }
            });
    }

    [After(Test)]
    public async Task DisposeServicesAsync() => await _services.DisposeAsync();
}
