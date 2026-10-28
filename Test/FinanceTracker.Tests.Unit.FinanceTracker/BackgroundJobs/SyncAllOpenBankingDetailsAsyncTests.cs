using FinanceTracker.BackgroundJobs;
using FinanceTracker.Contracts;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public class SyncAllOpenBankingDetailsAsyncTests : OpenBankingSyncJobTestBase
{
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
            await Assert.That(Synced).IsEquivalentTo([
                (userId, Scenario.ConnectionMappings["first"]),
                (userId, Scenario.ConnectionMappings["second"]),
                (otherUserId, Scenario.ConnectionMappings["theirs"])
            ]);
            await OpenBankingService.Received(3)
                .SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, CancellationToken);
        }
    }

    [Test]
    public async Task ExecuteAsync_WhenUsersHaveConnections_NotifiesEachSyncedUserOnce()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.UserScenario.WithUser("unconnected"))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("second", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[Username]);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[OtherUsername]);
            await SyncNotifier.DidNotReceive().NotifyAsync(Scenario.UserMappings["unconnected"]);
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

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await OpenBankingService.Received(3)
                .SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, CancellationToken);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[Username]);
        }
    }

    [Test]
    public async Task ExecuteAsync_AfterSyncing_MatchesEachOwnersTransfersOnceAsThem()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.UserScenario.WithUser("unconnected"))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("second", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        // Every connection has synced first, so transfers between a user's banks can pair
        await Assert.That(Matched).IsEquivalentTo([
            (Scenario.UserMappings[Username], 3),
            (Scenario.UserMappings[OtherUsername], 3)
        ]);
    }

    [Test]
    public async Task ExecuteAsync_WhenMatchingFails_ContinuesWithTheRestAndNotifies()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        TransferMatchingService.MatchChangesAsync(CancellationToken).ThrowsAsync(new InvalidOperationException("boom"));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.ExecuteAsync(TickerFunctionContextFactory.Create(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await TransferMatchingService.Received(2).MatchChangesAsync(CancellationToken);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[Username]);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[OtherUsername]);
        }
    }

    [Test]
    public async Task ExecuteAsync_WhenUsersHaveNoConnections_DoesNotSyncOrNotify()
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
            await OpenBankingService.DidNotReceiveWithAnyArgs().SyncConnectionAsync(default, default, default);
            await SyncNotifier.DidNotReceiveWithAnyArgs().NotifyAsync(default);
        }
    }

    private SyncAllOpenBankingDetailsAsync BuildSubject() => new(ScopeFactory, DbContextFactory, SyncNotifier);
}
