using FinanceTracker.Contracts;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Tests.Unit.Shared;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public abstract class OpenBankingSyncJobTestBase : InfrastructureTestBase
{
    protected const string Username = "primary";
    protected const string OtherUsername = "secondary";

    protected IOpenBankingService OpenBankingService { get; } = Substitute.For<IOpenBankingService>();
    protected ISyncNotifier SyncNotifier { get; } = Substitute.For<ISyncNotifier>();

    /// <summary>
    /// Each connection synced, with the user the service was acting as at the time.
    /// </summary>
    protected List<(Guid UserId, Guid ConnectionId)> Synced { get; } = [];

    [Before(Test)]
    public void SetupOpenBankingService()
    {
        Guid currentUser = Guid.Empty;

        OpenBankingService.RunFunctionAsUser(Arg.Any<Guid>(), Arg.Any<Func<Task>>())
            .Returns(async callInfo =>
            {
                currentUser = callInfo.Arg<Guid>();
                await callInfo.Arg<Func<Task>>()();
                currentUser = Guid.Empty;
            });

        OpenBankingService
            .When(x => x.SyncConnectionAsync(Arg.Any<Guid>(), Arg.Any<SyncTypes>(), Arg.Any<CancellationToken>()))
            .Do(callInfo => Synced.Add((currentUser, callInfo.Arg<Guid>())));
    }
}
