using FinanceTracker.BackgroundJobs;
using FinanceTracker.Contracts;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TickerQ.Utilities.Base;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public class SyncUserOpenBankingDetailsAsyncTests : OpenBankingSyncJobTestBase
{
    [Test]
    public async Task ExecuteAsync_SyncsOnlyTheRequestedUsersConnectionsAsThem()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("second", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        Guid userId = Scenario.UserMappings[Username];
        SyncUserOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(ContextFor(userId), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Synced).IsEquivalentTo([
                (userId, Scenario.ConnectionMappings["first"]),
                (userId, Scenario.ConnectionMappings["second"])
            ]);
            await OpenBankingService.Received(2)
                .SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, CancellationToken);
        }
    }

    [Test]
    public async Task ExecuteAsync_NotifiesOnlyTheRequestedUser()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("second", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        Guid userId = Scenario.UserMappings[Username];
        SyncUserOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(ContextFor(userId), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await SyncNotifier.Received(1).NotifyAsync(userId);
            await SyncNotifier.DidNotReceive().NotifyAsync(Scenario.UserMappings[OtherUsername]);
        }
    }

    [Test]
    public async Task ExecuteAsync_WhenAConnectionFailsToSync_ContinuesWithTheRestAndNotifiesTheUser()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("broken", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("third", Username));

        OpenBankingService.SyncConnectionAsync(Scenario.ConnectionMappings["broken"], SyncTypes.All, CancellationToken)
            .ThrowsAsync(new InvalidOperationException("boom"));

        Guid userId = Scenario.UserMappings[Username];
        SyncUserOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(ContextFor(userId), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await OpenBankingService.Received(3)
                .SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, CancellationToken);
            await SyncNotifier.Received(1).NotifyAsync(userId);
        }
    }

    [Test]
    public async Task ExecuteAsync_WhenTheUserHasNoConnections_StillNotifiesThem()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        Guid userId = Scenario.UserMappings[Username];
        SyncUserOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(ContextFor(userId), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await OpenBankingService.DidNotReceiveWithAnyArgs().SyncConnectionAsync(default, default, default);
            await SyncNotifier.Received(1).NotifyAsync(userId);
        }
    }

    private static TickerFunctionContext<SyncUserOpenBankingDetailsRequest> ContextFor(Guid userId) =>
        new(TickerFunctionContextFactory.Create(), new SyncUserOpenBankingDetailsRequest(userId));

    private SyncUserOpenBankingDetailsAsync BuildSubject() => new(ScopeFactory, DbContextFactory, SyncNotifier);
}
