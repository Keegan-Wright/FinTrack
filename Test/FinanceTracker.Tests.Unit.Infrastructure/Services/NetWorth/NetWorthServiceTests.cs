using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.NetWorth;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using DomainNetWorthSnapshot = FinanceTracker.Domain.NetWorth.NetWorthSnapshot;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.NetWorth;

public class NetWorthServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetNetWorthAsync_ListsTheRangeAndHowItsChangedThisMonthAndYear()
    {
        // Arrange
        await SeedAccountsAsync(Username);
        await AddBalancesAsync("current",
            (new DateOnly(2025, 12, 31), 1000m, false),
            (new DateOnly(2026, 2, 28), 1500m, true),
            (new DateOnly(2026, 3, 10), 1800m, false));

        // Act
        NetWorthHistory history = await BuildSubject(Username).GetNetWorthAsync(new DateOnly(2026, 3, 1),
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(history.Points.Select(static x => x.Date)).IsEquivalentTo([new DateOnly(2026, 3, 10)]);
            await Assert.That(history.Points[0].NetWorth).IsEqualTo(1800m);
            await Assert.That(history.ChangeThisMonth).IsEqualTo(300m);
            await Assert.That(history.ChangeThisYear).IsEqualTo(800m);
        }
    }

    [Test]
    public async Task GetNetWorthAsync_AllOfIt_MarksTheDaysWorkedOut()
    {
        // Arrange
        await SeedAccountsAsync(Username);
        await AddBalancesAsync("current", (new DateOnly(2026, 3, 9), 900m, true),
            (new DateOnly(2026, 3, 10), 1000m, false));

        // Act
        NetWorthHistory history = await BuildSubject(Username).GetNetWorthAsync(null, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(history.Points.Select(static x => x.IsDerived)).IsEquivalentTo([true, false],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(history.ChangeThisMonth).IsNull();
        }
    }

    [Test]
    public async Task GetNetWorthAsync_NoHistory_IsEmpty()
    {
        // Arrange
        await SeedAccountsAsync(Username);

        // Act
        NetWorthHistory history = await BuildSubject(Username).GetNetWorthAsync(null, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(history.Points).IsEmpty();
            await Assert.That(history.Breakdown).IsEmpty();
        }
    }

    [Test]
    public async Task GetNetWorthAsync_BreaksTodayDownIntoAccountsCardsAndDebts()
    {
        // Arrange
        await SeedAccountsAsync(Username)
            .Do(s => s.DebtScenario.WithDebt("loan", Username, userId =>
                new Debt(userId, "Car loan", null, 900m, new DateOnly(2026, 1, 1), 0m, 100m)));
        await AddBalancesAsync("current", (new DateOnly(2026, 3, 10), 500m, false));
        await AddBalancesAsync("card", (new DateOnly(2026, 3, 10), -40m, false));

        // Act
        NetWorthHistory history = await BuildSubject(Username).GetNetWorthAsync(null, CancellationToken);

        // Assert
        await Assert.That(history.Breakdown.Select(static x => (x.Name, x.Kind, x.Amount))).IsEquivalentTo(
            [("Car loan", NetWorthItemKind.Debt, -900m), ("Current", NetWorthItemKind.Account, 500m),
                ("Card", NetWorthItemKind.Card, -40m)],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task GetBalanceHistoriesAsync_ListsEachAccountsBalancesFromTheDay()
    {
        // Arrange
        await SeedAccountsAsync(Username);
        await AddBalancesAsync("current", (new DateOnly(2026, 3, 1), 10m, true),
            (new DateOnly(2026, 3, 9), 90m, false), (new DateOnly(2026, 3, 10), 100m, false));

        // Act
        List<AccountBalanceHistory> histories = await BuildSubject(Username)
            .GetBalanceHistoriesAsync(new DateOnly(2026, 3, 9), CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        AccountBalanceHistory current = histories.Single();
        using (Assert.Multiple())
        {
            await Assert.That(current.AccountId).IsEqualTo(Scenario.AccountMappings["current"]);
            await Assert.That(current.Points.Select(static x => x.Amount)).IsEquivalentTo([90m, 100m],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task GetBalanceHistoriesAsync_LeavesOutOtherUsersAccounts()
    {
        // Arrange
        await SeedAccountsAsync(OtherUsername);
        await AddBalancesAsync("current", (new DateOnly(2026, 3, 10), 100m, false));
        await Scenario.Do(s => s.UserScenario.WithUser(Username));

        // Act
        List<AccountBalanceHistory> histories = await BuildSubject(Username)
            .GetBalanceHistoriesAsync(null, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(histories).IsEmpty();
    }

    /// <summary>
    /// Each day's closing balance for the account, and the day's net worth from it alone.
    /// </summary>
    private async Task AddBalancesAsync(string accountName,
        params (DateOnly Day, decimal Current, bool Derived)[] days)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid accountId = Scenario.AccountMappings[accountName];
        BankAccount account = context.BankAccounts.Single(x => x.Id == accountId);

        foreach ((DateOnly day, decimal current, bool derived) in days)
        {
            BalanceSnapshotSource source = derived ? BalanceSnapshotSource.Derived : BalanceSnapshotSource.Synced;
            BalanceSnapshot balance = new(account.UserId, accountId, day,
                new AccountBalance(current, current, day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)), source,
                isStale: false);
            context.BalanceSnapshots.Add(balance);

            if (context.NetWorthSnapshots.Local.All(x => x.Date != day) &&
                !context.NetWorthSnapshots.Any(x => x.UserId == account.UserId && x.Date == day))
            {
                context.NetWorthSnapshots.Add(new DomainNetWorthSnapshot(account.UserId, day, [balance]));
            }
        }

        await context.SaveChangesAsync(CancellationToken);
    }

    private Task<TestingScenario> SeedAccountsAsync(string username) =>
        Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username))
            .Do(s => s.BankAccountScenario.WithAccount("current", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-current", "TRANSACTION", "Current", "GBP")))
            .Do(s => s.BankAccountScenario.WithAccount("card", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-card", "CREDIT_CARD", "Card", "GBP",
                    BankAccountKind.Card)));

    private NetWorthService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<NetWorthService>.Instance,
            new FixedTimeProvider(Now), new AccountScope(CurrentUserFor(username)));
}
