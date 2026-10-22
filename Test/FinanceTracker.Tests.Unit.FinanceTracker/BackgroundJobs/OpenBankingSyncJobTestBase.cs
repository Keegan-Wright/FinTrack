using FinanceTracker.Contracts;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public abstract class OpenBankingSyncJobTestBase : InfrastructureTestBase
{
    protected const string Username = "primary";
    protected const string OtherUsername = "secondary";

    private ServiceProvider _services = null!;

    /// <summary>
    /// The user of the scope <see cref="OpenBankingService"/> was last resolved from.
    /// </summary>
    private ICurrentUser? _resolvedFor;

    protected IOpenBankingSyncService OpenBankingService { get; } = Substitute.For<IOpenBankingSyncService>();
    protected ISyncNotifier SyncNotifier { get; } = Substitute.For<ISyncNotifier>();

    /// <summary>
    /// Scopes the jobs resolve <see cref="OpenBankingService"/> from, with a real <see cref="CurrentUser"/>.
    /// </summary>
    protected IServiceScopeFactory ScopeFactory => _services.GetRequiredService<IServiceScopeFactory>();

    /// <summary>
    /// Each connection synced, with the user the service's scope was acting as at the time.
    /// </summary>
    protected List<(Guid UserId, Guid ConnectionId)> Synced { get; } = [];

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
            .BuildServiceProvider();

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
