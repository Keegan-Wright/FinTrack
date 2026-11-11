using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.Balances;
using FinanceTracker.Telemetry;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.OpenBanking;

public class BalanceSnapshotServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateOnly Day = new(2026, 10, 27);
    private static readonly DateTime Morning = new(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Evening = new(2026, 10, 27, 20, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task SnapshotAsync_SavesEachSyncedBalanceAndTheirNetWorth()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(-100m, Morning)))
            .Do(s => s.BankAccountScenario.WithBalance("mine-savings", Balance(500m, Morning)))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-current", SyncType.Balance, Morning))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-savings", SyncType.Balance, Morning.AddSeconds(5)));

        BalanceSnapshotService subject = BuildSubject(Username);

        // Act
        NetWorthSnapshot? netWorth = await subject.SnapshotAsync(Day, Morning, CancellationToken);

        // Assert
        List<BalanceSnapshot> balances = await LoadBalancesAsync();
        BalanceSnapshot current = SnapshotOf(balances, "mine-current");

        using (Assert.Multiple())
        {
            await Assert.That(balances).Count().IsEqualTo(2);
            await Assert.That(current.Date).IsEqualTo(Day);
            await Assert.That(current.Balance).IsEqualTo(Balance(-100m, Morning));
            await Assert.That(current.Source).IsEqualTo(BalanceSnapshotSource.Synced);
            await Assert.That(current.IsStale).IsFalse();
            await Assert.That(netWorth!.Assets).IsEqualTo(500m);
            await Assert.That(netWorth.Liabilities).IsEqualTo(100m);
            await Assert.That(netWorth.IsStale).IsFalse();
            await Assert.That(await LoadNetWorthAsync()).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task SnapshotAsync_DebtsNotOnAnAccount_CountAsLiabilities()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(-100m, Morning)))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-current", SyncType.Balance, Morning))
            .Do(s => s.DebtScenario.WithDebt("loan", Username, static userId =>
                new Debt(userId, "Car loan", null, 4000m, new DateOnly(2026, 1, 1), 0m, 200m)))
            .Do(s => s.DebtScenario.WithDebt("card", Username, static userId =>
                new Debt(userId, "Card", null, 900m, new DateOnly(2026, 1, 1), 19.9m, 50m)));

        // The card's balance is already in its account's
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            Guid cardId = Scenario.DebtMappings["card"];
            Guid accountId = Scenario.AccountMappings["mine-current"];
            Debt card = await context.Debts.SingleAsync(x => x.Id == cardId, CancellationToken);
            card.LinkAccount(await context.BankAccounts.SingleAsync(x => x.Id == accountId, CancellationToken));
            await context.SaveChangesAsync(CancellationToken);
        }

        BalanceSnapshotService subject = BuildSubject(Username);

        // Act
        NetWorthSnapshot? netWorth = await subject.SnapshotAsync(Day, Morning, CancellationToken);

        // Assert
        await Assert.That(netWorth!.Liabilities).IsEqualTo(4100m);
    }

    [Test]
    public async Task SnapshotAsync_LaterSyncTheSameDay_ReplacesRatherThanDuplicates()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(40m, Morning)))
            .Do(s => s.BankAccountScenario.WithBalance("mine-savings", Balance(60m, Morning)))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-current", SyncType.Balance, Morning))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-savings", SyncType.Balance, Morning));

        BalanceSnapshotService subject = BuildSubject(Username);
        await subject.SnapshotAsync(Day, Morning, CancellationToken);

        await Scenario.Do(s => s.BankAccountScenario.WithBalance("mine-savings", Balance(75m, Evening)))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-current", SyncType.Balance, Evening))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-savings", SyncType.Balance, Evening));

        // Act
        NetWorthSnapshot? netWorth = await subject.SnapshotAsync(Day, Evening, CancellationToken);

        // Assert
        List<BalanceSnapshot> balances = await LoadBalancesAsync();
        List<NetWorthSnapshot> netWorths = await LoadNetWorthAsync();

        using (Assert.Multiple())
        {
            await Assert.That(balances).Count().IsEqualTo(2);
            await Assert.That(SnapshotOf(balances, "mine-savings").Balance).IsEqualTo(Balance(75m, Evening));
            await Assert.That(netWorths).Count().IsEqualTo(1);
            await Assert.That(netWorths[0].Assets).IsEqualTo(115m);
            await Assert.That(netWorth!.Id).IsEqualTo(netWorths[0].Id);
        }
    }

    [Test]
    public async Task SnapshotAsync_BalanceNotSynced_KeepsTheDaysEarlierSnapshot()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(40m, Morning)))
            .Do(s => s.BankAccountScenario.WithBalance("mine-savings", Balance(60m, Morning)))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-current", SyncType.Balance, Morning))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-savings", SyncType.Balance, Morning));

        BalanceSnapshotService subject = BuildSubject(Username);
        await subject.SnapshotAsync(Day, Morning, CancellationToken);

        // The evening sync refreshes the current account, the savings balance is refused
        await Scenario.Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(30m, Evening)))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-current", SyncType.Balance, Evening))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-savings", SyncType.Transactions, Evening));

        // Act
        NetWorthSnapshot? netWorth = await subject.SnapshotAsync(Day, Evening, CancellationToken);

        // Assert
        List<BalanceSnapshot> balances = await LoadBalancesAsync();
        BalanceSnapshot savings = SnapshotOf(balances, "mine-savings");

        using (Assert.Multiple())
        {
            await Assert.That(SnapshotOf(balances, "mine-current").Balance).IsEqualTo(Balance(30m, Evening));
            await Assert.That(savings.Balance).IsEqualTo(Balance(60m, Morning));
            await Assert.That(savings.IsStale).IsFalse();
            await Assert.That(netWorth!.Assets).IsEqualTo(90m);
            await Assert.That(netWorth.IsStale).IsFalse();
        }
    }

    [Test]
    public async Task SnapshotAsync_BalanceNotSyncedAndNoSnapshotYet_SavesTheStoredOneAsStale()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(40m, Morning)))
            .Do(s => s.BankAccountScenario.WithBalance("mine-savings", Balance(60m, Morning.AddDays(-1))))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-current", SyncType.Balance, Morning))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-savings", SyncType.Balance, Morning.AddDays(-1)));

        BalanceSnapshotService subject = BuildSubject(Username);

        // Act
        NetWorthSnapshot? netWorth = await subject.SnapshotAsync(Day, Morning, CancellationToken);

        // Assert
        List<BalanceSnapshot> balances = await LoadBalancesAsync();
        BalanceSnapshot savings = SnapshotOf(balances, "mine-savings");

        using (Assert.Multiple())
        {
            await Assert.That(SnapshotOf(balances, "mine-current").IsStale).IsFalse();
            await Assert.That(savings.IsStale).IsTrue();
            await Assert.That(savings.Balance.Current).IsEqualTo(60m);
            await Assert.That(netWorth!.Assets).IsEqualTo(100m);
            await Assert.That(netWorth.IsStale).IsTrue();
        }
    }

    [Test]
    public async Task SnapshotAsync_StaleSnapshotThenSynced_IsNoLongerStale()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(40m, Morning.AddDays(-1))));

        BalanceSnapshotService subject = BuildSubject(Username);
        await subject.SnapshotAsync(Day, Morning, CancellationToken);

        await Scenario.Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(45m, Evening)))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-current", SyncType.Balance, Evening));

        // Act
        NetWorthSnapshot? netWorth = await subject.SnapshotAsync(Day, Evening, CancellationToken);

        // Assert
        BalanceSnapshot current = SnapshotOf(await LoadBalancesAsync(), "mine-current");

        using (Assert.Multiple())
        {
            await Assert.That(current.Balance).IsEqualTo(Balance(45m, Evening));
            await Assert.That(current.IsStale).IsFalse();
            await Assert.That(netWorth!.IsStale).IsFalse();
        }
    }

    [Test]
    public async Task SnapshotAsync_KeepsEachDaySeparate()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(40m, Morning)));

        BalanceSnapshotService subject = BuildSubject(Username);
        await subject.SnapshotAsync(Day.AddDays(-1), Morning.AddDays(-1), CancellationToken);

        // Act
        await subject.SnapshotAsync(Day, Morning, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((await LoadBalancesAsync()).Select(x => x.Date))
                .IsEquivalentTo([Day.AddDays(-1), Day]);
            await Assert.That((await LoadNetWorthAsync()).Select(x => x.Date))
                .IsEquivalentTo([Day.AddDays(-1), Day]);
        }
    }

    [Test]
    public async Task SnapshotAsync_LeavesOutAccountsWithoutABalance()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-savings", Balance(60m, Morning)));

        BalanceSnapshotService subject = BuildSubject(Username);

        // Act
        await subject.SnapshotAsync(Day, Morning, CancellationToken);

        // Assert
        await Assert.That((await LoadBalancesAsync()).Select(x => x.AccountId))
            .IsEquivalentTo([Scenario.AccountMappings["mine-savings"]]);
    }

    [Test]
    public async Task SnapshotAsync_NoBalancesYet_SavesNothing()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine");

        BalanceSnapshotService subject = BuildSubject(Username);

        // Act
        NetWorthSnapshot? netWorth = await subject.SnapshotAsync(Day, Morning, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(netWorth).IsNull();
            await Assert.That(await LoadBalancesAsync()).IsEmpty();
            await Assert.That(await LoadNetWorthAsync()).IsEmpty();
        }
    }

    [Test]
    public async Task SnapshotAsync_LeavesOtherUsersAccountsAlone()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(40m, Morning)));
        await SeedAccountsAsync(OtherUsername, "theirs")
            .Do(s => s.BankAccountScenario.WithBalance("theirs-current", Balance(900m, Morning)));

        BalanceSnapshotService subject = BuildSubject(Username);

        // Act
        NetWorthSnapshot? netWorth = await subject.SnapshotAsync(Day, Morning, CancellationToken);

        // Assert
        Guid userId = Scenario.UserMappings[Username];

        using (Assert.Multiple())
        {
            await Assert.That(netWorth!.Assets).IsEqualTo(40m);
            await Assert.That((await LoadBalancesAsync()).All(x => x.UserId == userId)).IsTrue();
            await Assert.That((await LoadNetWorthAsync()).Select(x => x.UserId)).IsEquivalentTo([userId]);
        }
    }

    [Test]
    public async Task BackfillAsync_WorksOutPastBalancesAndTheirNetWorthOnce()
    {
        // Arrange, 500 today after 100 came in yesterday
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-savings", Balance(500m, Morning)))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-savings", SyncType.Balance, Morning))
            .Do(s => s.TransactionScenario.WithTransaction("in", "mine-savings", Settled(100m, Day.AddDays(-1))));
        BalanceSnapshotService subject = BuildSubject(Username);
        await subject.SnapshotAsync(Day, Morning, CancellationToken);

        using SpanRecorder spans = new();

        // Act
        int days = await subject.BackfillAsync(CancellationToken);
        int again;
        using (spans.StartRoot())
        {
            again = await subject.BackfillAsync(CancellationToken);
        }

        // Assert
        Dictionary<DateOnly, BalanceSnapshot> balances = (await LoadBalancesAsync()).ToDictionary(static x => x.Date);
        Dictionary<DateOnly, NetWorthSnapshot> netWorth = (await LoadNetWorthAsync()).ToDictionary(static x => x.Date);
        using (Assert.Multiple())
        {
            await Assert.That(days).IsEqualTo(2);
            await Assert.That(again).IsEqualTo(0);
            await Assert.That(spans.Root.GetTagItem(TelemetryTags.Days)).IsEqualTo(0);
            await Assert.That(balances[Day.AddDays(-1)].Balance.Current).IsEqualTo(500m);
            await Assert.That(balances[Day.AddDays(-2)].Balance.Current).IsEqualTo(400m);
            await Assert.That(balances[Day.AddDays(-2)].Source).IsEqualTo(BalanceSnapshotSource.Derived);
            await Assert.That(netWorth[Day.AddDays(-2)].NetWorth).IsEqualTo(400m);
            await Assert.That(netWorth[Day.AddDays(-2)].IsDerived).IsTrue();
            await Assert.That(netWorth[Day].IsDerived).IsFalse();
        }
    }

    [Test]
    public async Task BackfillAsync_ANewAccountsHistory_AddsToEarlierDaysNetWorth()
    {
        // Arrange, savings synced yesterday before the current account had a balance
        DateOnly yesterday = Day.AddDays(-1);
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.BankAccountScenario.WithBalance("mine-savings", Balance(500m, Morning.AddDays(-1))))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-savings", SyncType.Balance, Morning.AddDays(-1)));
        BalanceSnapshotService subject = BuildSubject(Username);
        await subject.SnapshotAsync(yesterday, Morning.AddDays(-1), CancellationToken);
        await subject.BackfillAsync(CancellationToken);

        await Scenario.Do(s => s.BankAccountScenario.WithBalance("mine-current", Balance(80m, Morning)))
            .Do(s => s.BankAccountScenario.WithSyncLog("mine-current", SyncType.Balance, Morning))
            .Do(s => s.TransactionScenario.WithTransaction("spent", "mine-current", Settled(-20m, Day)));
        await subject.SnapshotAsync(Day, Morning, CancellationToken);

        // Act
        await subject.BackfillAsync(CancellationToken);

        // Assert, yesterday's 500 plus the current account's 100 before today's spend
        NetWorthSnapshot before = (await LoadNetWorthAsync()).Single(x => x.Date == yesterday);
        using (Assert.Multiple())
        {
            await Assert.That(before.NetWorth).IsEqualTo(600m);
            await Assert.That(before.IsDerived).IsTrue();
        }
    }

    [Test]
    public async Task BackfillAsync_NoAccountsWithABalance_DoesNothing()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine");

        // Act & Assert
        await Assert.That(await BuildSubject(Username).BackfillAsync(CancellationToken)).IsEqualTo(0);
    }

    private static TransactionDetails Settled(decimal amount, DateOnly day) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            OccurredAt = day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
            IsPending = false
        };

    private static AccountBalance Balance(decimal amount, DateTime asOf) => new(amount, amount, asOf);

    private BalanceSnapshot SnapshotOf(List<BalanceSnapshot> balances, string accountName) =>
        balances.Single(x => x.AccountId == Scenario.AccountMappings[accountName]);

    private Task<TestingScenario> SeedAccountsAsync(string username, string prefix) =>
        Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection(prefix, username))
            .Do(s => s.BankAccountScenario.WithAccount($"{prefix}-current", prefix))
            .Do(s => s.BankAccountScenario.WithAccount($"{prefix}-savings", prefix));

    private async Task<List<BalanceSnapshot>> LoadBalancesAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BalanceSnapshots.ToListAsync(CancellationToken);
    }

    private async Task<List<NetWorthSnapshot>> LoadNetWorthAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.NetWorthSnapshots.ToListAsync(CancellationToken);
    }

    private BalanceSnapshotService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<BalanceSnapshotService>.Instance,
            new FixedTimeProvider(Evening), TestMetrics.Unobserved);
}
