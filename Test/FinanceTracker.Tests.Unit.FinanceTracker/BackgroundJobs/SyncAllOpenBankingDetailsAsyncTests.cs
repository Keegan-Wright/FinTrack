using FinanceTracker.BackgroundJobs;
using FinanceTracker.Contracts;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public class SyncAllOpenBankingDetailsAsyncTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private readonly IOpenBankingService _openBankingService = Substitute.For<IOpenBankingService>();
    private readonly IBackgroundSyncService _syncService = Substitute.For<IBackgroundSyncService>();

    /// <summary>
    /// Each connection synced, with the user the service was acting as at the time.
    /// </summary>
    private readonly List<(Guid UserId, Guid ConnectionId)> _synced = [];

    [Before(Test)]
    public void SetupOpenBankingService()
    {
        Guid currentUser = Guid.Empty;

        _openBankingService.RunFunctionAsUser(Arg.Any<Guid>(), Arg.Any<Func<Task>>())
            .Returns(async callInfo =>
            {
                currentUser = callInfo.Arg<Guid>();
                await callInfo.Arg<Func<Task>>()();
                currentUser = Guid.Empty;
            });

        _openBankingService
            .When(x => x.SyncConnectionAsync(Arg.Any<Guid>(), Arg.Any<SyncTypes>(), Arg.Any<CancellationToken>()))
            .Do(callInfo => _synced.Add((currentUser, callInfo.Arg<Guid>())));
    }

    [Test]
    public async Task ExecuteAsync_WhenUsersHaveConnections_SyncsEachConnectionAsItsOwner()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("second", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        Guid userId = Scenario.UserMappings[Username];
        Guid otherUserId = Scenario.UserMappings[OtherUsername];

        using (Assert.Multiple())
        {
            await Assert.That(_synced).IsEquivalentTo([
                (userId, Scenario.ConnectionMappings["first"]),
                (userId, Scenario.ConnectionMappings["second"]),
                (otherUserId, Scenario.ConnectionMappings["theirs"])
            ]);
            await _openBankingService.Received(3)
                .SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, CancellationToken);
        }
    }

    [Test]
    public async Task ExecuteAsync_WhenUsersHaveConnections_NotifiesCompleteOnce()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("second", Username));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        await _syncService.Received(1).NotifySyncComplete();
    }

    [Test]
    public async Task ExecuteAsync_WhenAConnectionFailsToSync_ContinuesWithTheRestAndNotifiesComplete()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("broken", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("third", Username));

        _openBankingService.SyncConnectionAsync(Scenario.ConnectionMappings["broken"], SyncTypes.All, CancellationToken)
            .ThrowsAsync(new InvalidOperationException("boom"));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await _openBankingService.Received(3)
                .SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, CancellationToken);
            await _syncService.Received(1).NotifySyncComplete();
        }
    }

    [Test]
    public async Task ExecuteAsync_WhenUsersHaveNoConnections_DoesNotSyncAndStillNotifiesComplete()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await _openBankingService.DidNotReceiveWithAnyArgs().RunFunctionAsUser(default, default!);
            await _openBankingService.DidNotReceiveWithAnyArgs().SyncConnectionAsync(default, default, default);
            await _syncService.Received(1).NotifySyncComplete();
        }
    }

    private SyncAllOpenBankingDetailsAsync BuildSubject() => new(_openBankingService, DbContextFactory, _syncService);
}
