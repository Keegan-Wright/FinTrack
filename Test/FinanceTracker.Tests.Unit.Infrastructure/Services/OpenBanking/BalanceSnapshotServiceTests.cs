using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.Balances;
using FinanceTracker.Tests.Unit.Shared;
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
        new(CurrentUserFor(username), DbContextFactory, NullLogger<BalanceSnapshotService>.Instance);
}
