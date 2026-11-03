using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using DomainNetWorthSnapshot = FinanceTracker.Domain.NetWorth.NetWorthSnapshot;

namespace FinanceTracker.Tests.Integration.Api;

public class NetWorthApiTests : IntegrationTestBase
{
    [Test]
    public async Task GetNetWorth_FromADay_ListsOnlyTheDaysFromIt()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));
        await AddBalanceAsync(user, new DateOnly(2026, 1, 10), 100m, BalanceSnapshotSource.Derived);
        await AddBalanceAsync(user, new DateOnly(2026, 2, 10), 200m, BalanceSnapshotSource.Synced);
        INetWorthService service = ApiFor(user).Get<INetWorthService>();

        // Act
        NetWorthHistory history = await service.GetNetWorthAsync(new DateOnly(2026, 2, 1), CancellationToken);
        List<AccountBalanceHistory> balances = await service.GetBalanceHistoriesAsync(null, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(history.Points.Select(static x => x.NetWorth)).IsEquivalentTo([200m]);
            await Assert.That(history.Breakdown.Single().Amount).IsEqualTo(200m);
            await Assert.That(balances.Single().Points.Select(static x => x.IsDerived)).IsEquivalentTo(
                [true, false], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    private async Task AddBalanceAsync(SignedInUser user, DateOnly day, decimal current,
        BalanceSnapshotSource source)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid userId = Scenario.UserMappings[user.Username];
        BalanceSnapshot balance = new(userId, Scenario.AccountMappings["main"], day,
            new AccountBalance(current, current, day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)), source,
            isStale: false);
        context.BalanceSnapshots.Add(balance);
        context.NetWorthSnapshots.Add(new DomainNetWorthSnapshot(userId, day, [balance]));
        await context.SaveChangesAsync(CancellationToken);
    }
}
