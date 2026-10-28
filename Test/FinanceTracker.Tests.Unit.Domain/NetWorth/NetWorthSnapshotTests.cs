using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Balances;

namespace FinanceTracker.Tests.Unit.Domain.NetWorth;

[Category("Domain")]
public class NetWorthSnapshotTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateOnly Day = new(2026, 10, 27);
    private static readonly DateTime AsOf = new(2026, 10, 27, 23, 30, 0, DateTimeKind.Utc);

    [Test]
    public async Task NetWorthSnapshot_TotalsCreditAsAssetsAndOverdraftsAsLiabilities()
    {
        NetWorthSnapshot snapshot = new(UserId, Day, [Balance(1200m), Balance(300.50m), Balance(-150m)]);

        using (Assert.Multiple())
        {
            await Assert.That(snapshot.Assets).IsEqualTo(1500.50m);
            await Assert.That(snapshot.Liabilities).IsEqualTo(150m);
            await Assert.That(snapshot.NetWorth).IsEqualTo(1350.50m);
            await Assert.That(snapshot.IsStale).IsFalse();
        }
    }

    [Test]
    public async Task NetWorthSnapshot_UsesTheCurrentBalanceNotTheAvailableOne()
    {
        var balance = new BalanceSnapshot(UserId, Guid.CreateVersion7(), Day,
            new AccountBalance(Available: 1500m, Current: 100m, AsOf), BalanceSnapshotSource.Synced, isStale: false);

        NetWorthSnapshot snapshot = new(UserId, Day, [balance]);

        await Assert.That(snapshot.Assets).IsEqualTo(100m);
    }

    [Test]
    public async Task NetWorthSnapshot_IsStaleWhenAnyBalanceIs()
    {
        NetWorthSnapshot snapshot = new(UserId, Day, [Balance(100m), Balance(50m, isStale: true)]);

        await Assert.That(snapshot.IsStale).IsTrue();
    }

    [Test]
    public async Task NetWorthSnapshot_NoBalances_IsZero()
    {
        NetWorthSnapshot snapshot = new(UserId, Day, []);

        using (Assert.Multiple())
        {
            await Assert.That(snapshot.Assets).IsEqualTo(0m);
            await Assert.That(snapshot.Liabilities).IsEqualTo(0m);
            await Assert.That(snapshot.NetWorth).IsEqualTo(0m);
        }
    }

    [Test]
    public async Task NetWorthSnapshot_Total_ReplacesThePreviousTotals()
    {
        NetWorthSnapshot snapshot = new(UserId, Day, [Balance(100m, isStale: true), Balance(-20m)]);

        snapshot.Total([Balance(250m)]);

        using (Assert.Multiple())
        {
            await Assert.That(snapshot.Assets).IsEqualTo(250m);
            await Assert.That(snapshot.Liabilities).IsEqualTo(0m);
            await Assert.That(snapshot.IsStale).IsFalse();
        }
    }

    [Test]
    public async Task NetWorthSnapshot_Total_RejectsABalanceForAnotherDay()
    {
        NetWorthSnapshot snapshot = new(UserId, Day, []);
        var otherDay = new BalanceSnapshot(UserId, Guid.CreateVersion7(), Day.AddDays(-1),
            new AccountBalance(10m, 10m, AsOf), BalanceSnapshotSource.Synced, isStale: false);

        await Assert.That(() => snapshot.Total([otherDay])).Throws<ArgumentException>();
    }

    private static BalanceSnapshot Balance(decimal current, bool isStale = false) =>
        new(UserId, Guid.CreateVersion7(), Day, new AccountBalance(current, current, AsOf),
            BalanceSnapshotSource.Synced, isStale);
}
