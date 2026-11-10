using FinanceTracker.BackgroundJobs;
using FinanceTracker.Contracts;
using FinanceTracker.Telemetry;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

public class SyncAllOpenBankingDetailsAsyncTests : OpenBankingSyncJobTestBase
{
    [Test]
    public async Task Execute_WhenUsersHaveConnections_SyncsEachConnectionAsItsOwner()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("second", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

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
    public async Task Execute_WhenUsersHaveConnections_TellsEachOwnerAsItStartsBeforeSyncing()
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
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert, once each, before the first connection syncs
        using (Assert.Multiple())
        {
            await SyncNotifier.Received(1).NotifyStartedAsync(Scenario.UserMappings[Username]);
            await SyncNotifier.Received(1).NotifyStartedAsync(Scenario.UserMappings[OtherUsername]);
            await SyncNotifier.DidNotReceive().NotifyStartedAsync(Scenario.UserMappings["unconnected"]);
            Received.InOrder(() =>
            {
                SyncNotifier.NotifyStartedAsync(Arg.Any<Guid>());
                SyncNotifier.NotifyStartedAsync(Arg.Any<Guid>());
                OpenBankingService.SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, Arg.Any<CancellationToken>());
                OpenBankingService.SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, Arg.Any<CancellationToken>());
                OpenBankingService.SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, Arg.Any<CancellationToken>());
            });
        }
    }

    [Test]
    public async Task Execute_WhenUsersHaveConnections_NotifiesEachSyncedUserOnce()
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
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[Username]);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[OtherUsername]);
            await SyncNotifier.DidNotReceive().NotifyAsync(Scenario.UserMappings["unconnected"]);
        }
    }

    [Test]
    public async Task Execute_WhenAConnectionFailsToSync_ContinuesWithTheRestAndNotifiesTheUser()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("broken", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("third", Username));

        OpenBankingService.SyncConnectionAsync(Scenario.ConnectionMappings["broken"], SyncTypes.All, CancellationToken)
            .ThrowsAsync(new InvalidOperationException("boom"));

        using TestMetrics metrics = new();
        using MetricCollector<double> durations = metrics.Collect<double>(FinTrackMetrics.ConnectionSyncDurationName);
        SyncAllOpenBankingDetailsAsync subject = BuildSubject(metrics.Metrics);

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await OpenBankingService.Received(3)
                .SyncConnectionAsync(Arg.Any<Guid>(), SyncTypes.All, CancellationToken);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[Username]);
            await Assert.That(durations.GetMeasurementSnapshot()
                    .Select(static x => x.Tags[TelemetryTags.Outcome]!.ToString()!))
                .IsEquivalentTo(["succeeded", "failed", "succeeded"]);
        }
    }

    [Test]
    public async Task Execute_AfterSyncing_MatchesEachOwnersTransfersOnceAsThem()
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
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert
        // Every connection has synced first, so transfers between a user's banks can pair
        await Assert.That(Matched).IsEquivalentTo([
            (Scenario.UserMappings[Username], 3),
            (Scenario.UserMappings[OtherUsername], 3)
        ]);
    }

    [Test]
    public async Task Execute_WhenMatchingFails_ContinuesWithTheRestAndNotifies()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        TransferMatchingService.MatchChangesAsync(CancellationToken).ThrowsAsync(new InvalidOperationException("boom"));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await TransferMatchingService.Received(2).MatchChangesAsync(CancellationToken);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[Username]);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[OtherUsername]);
        }
    }

    [Test]
    public async Task Execute_AfterSyncingAndMatching_SnapshotsEachOwnersBalancesForToday()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("first", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("second", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert
        // Every connection has synced first, so net worth covers all of a user's banks, and the rules have run, so it
        // counts the debt payments they linked
        await Assert.That(Snapshots).IsEquivalentTo([
            (Scenario.UserMappings[Username], Today, Now, 3, true),
            (Scenario.UserMappings[OtherUsername], Today, Now, 3, true)
        ]);
    }

    [Test]
    public async Task Execute_WhenSnapshotFails_StillMatchesAndNotifies()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        BalanceSnapshotService.SnapshotAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime>(), CancellationToken)
            .ThrowsAsync(new InvalidOperationException("boom"));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Snapshots).Count().IsEqualTo(2);
            await TransferMatchingService.Received(2).MatchChangesAsync(CancellationToken);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[Username]);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[OtherUsername]);
        }
    }

    [Test]
    public async Task Execute_Synced_TellsEachOwnerOfBillsBudgetsPaymentsAndBalancesLast()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert, as each owner, once their balances are snapshot
        Guid mine = Scenario.UserMappings[Username];
        Guid theirs = Scenario.UserMappings[OtherUsername];
        await Assert.That(Told).IsEquivalentTo([
            (mine, "bills", true), (mine, "budgets", true), (mine, "payments", true), (mine, "balances", true),
            (theirs, "bills", true), (theirs, "budgets", true), (theirs, "payments", true), (theirs, "balances", true)
        ], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Execute_BillNoticesFail_StillTellsOfTheRest()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", Username));
        BillNotices.NotifyAsync(CancellationToken).ThrowsAsync(new InvalidOperationException("boom"));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert, bills were tried and threw, the rest still ran after
        await Assert.That(Told.Select(static x => x.Notices)).IsEquivalentTo(
            ["bills", "budgets", "payments", "balances"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Execute_AfterSnapshotting_BackfillsEachOwnersBalanceHistory()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));
        List<string> calls = [];
        BalanceSnapshotService
            .When(x => x.SnapshotAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()))
            .Do(_ => calls.Add("snapshot"));
        BalanceSnapshotService.When(x => x.BackfillAsync(Arg.Any<CancellationToken>()))
            .Do(_ => calls.Add("backfill"));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert
        await Assert.That(calls).IsEquivalentTo(["snapshot", "backfill", "snapshot", "backfill"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Execute_AfterSyncing_AppliesEachOwnersRulesBeforeMatching()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert, from when the sync started so a user's first run covers it
        await Assert.That(RulesApplied).IsEquivalentTo([
            (Scenario.UserMappings[Username], Now, false),
            (Scenario.UserMappings[OtherUsername], Now, false)
        ]);
    }

    [Test]
    public async Task Execute_WhenRulesFail_StillMatchesAndNotifies()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", Username));

        RuleApplicationService.ApplyChangesAsync(Arg.Any<DateTime>(), CancellationToken)
            .ThrowsAsync(new InvalidOperationException("boom"));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await TransferMatchingService.Received(1).MatchChangesAsync(CancellationToken);
            await SyncNotifier.Received(1).NotifyAsync(Scenario.UserMappings[Username]);
        }
    }

    [Test]
    public async Task Execute_WhenUsersHaveNoConnections_DoesNotSyncOrNotify()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername));

        SyncAllOpenBankingDetailsAsync subject = BuildSubject();

        // Act
        await subject.Execute(JobContexts.Scheduled(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await OpenBankingService.DidNotReceiveWithAnyArgs().SyncConnectionAsync(default, default, default);
            await SyncNotifier.DidNotReceiveWithAnyArgs().NotifyAsync(default);
        }
    }

    private SyncAllOpenBankingDetailsAsync BuildSubject(FinTrackMetrics? metrics = null) =>
        new(ScopeFactory, DbContextFactory, SyncNotifier, Clock, NullLogger<OpenBankingSyncJob>.Instance,
            metrics ?? TestMetrics.Unobserved);
}
