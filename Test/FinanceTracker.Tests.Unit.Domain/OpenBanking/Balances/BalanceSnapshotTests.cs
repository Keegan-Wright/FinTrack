using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Balances;

[Category("Domain")]
public class BalanceSnapshotTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateOnly Day = new(2026, 10, 27);
    private static readonly DateTime Morning = new(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Evening = new(2026, 10, 27, 20, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task BankAccount_SnapshotBalance_Synced_TakesTheBalance()
    {
        BankAccount account = AccountWithBalance(40m, Morning);

        BalanceSnapshot snapshot = account.SnapshotBalance(Day, synced: true);

        using (Assert.Multiple())
        {
            await Assert.That(snapshot.UserId).IsEqualTo(UserId);
            await Assert.That(snapshot.AccountId).IsEqualTo(account.Id);
            await Assert.That(snapshot.Date).IsEqualTo(Day);
            await Assert.That(snapshot.Balance).IsEqualTo(new AccountBalance(40m, 40m, Morning));
            await Assert.That(snapshot.Source).IsEqualTo(BalanceSnapshotSource.Synced);
            await Assert.That(snapshot.IsStale).IsFalse();
        }
    }

    [Test]
    public async Task BankAccount_SnapshotBalance_NotSynced_IsStale()
    {
        BankAccount account = AccountWithBalance(40m, Morning.AddDays(-1));

        BalanceSnapshot snapshot = account.SnapshotBalance(Day, synced: false);

        using (Assert.Multiple())
        {
            await Assert.That(snapshot.Balance.Current).IsEqualTo(40m);
            await Assert.That(snapshot.IsStale).IsTrue();
        }
    }

    [Test]
    public async Task BankAccount_SnapshotBalance_CopiesTheBalance()
    {
        BankAccount account = AccountWithBalance(40m, Morning);

        BalanceSnapshot snapshot = account.SnapshotBalance(Day, synced: true);

        await Assert.That(ReferenceEquals(snapshot.Balance, account.Balance)).IsFalse();
    }

    [Test]
    public async Task BankAccount_SnapshotBalance_NoBalance_Throws()
    {
        BankAccount account = new(UserId, Guid.CreateVersion7(), "acc-1", "TRANSACTION", "Current", "GBP");

        await Assert.That(() => account.SnapshotBalance(Day, synced: true)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task BalanceSnapshot_Refresh_Synced_TakesTheNewBalance()
    {
        BankAccount account = AccountWithBalance(40m, Morning);
        BalanceSnapshot snapshot = account.SnapshotBalance(Day, synced: true);
        account.UpdateBalance(30m, 30m, Evening);

        snapshot.Refresh(account, synced: true);

        using (Assert.Multiple())
        {
            await Assert.That(snapshot.Balance).IsEqualTo(new AccountBalance(30m, 30m, Evening));
            await Assert.That(snapshot.Date).IsEqualTo(Day);
            await Assert.That(snapshot.IsStale).IsFalse();
        }
    }

    [Test]
    public async Task BalanceSnapshot_Refresh_NotSynced_KeepsTheEarlierBalance()
    {
        BankAccount account = AccountWithBalance(40m, Morning);
        BalanceSnapshot snapshot = account.SnapshotBalance(Day, synced: true);
        account.UpdateBalance(30m, 30m, Evening);

        snapshot.Refresh(account, synced: false);

        using (Assert.Multiple())
        {
            await Assert.That(snapshot.Balance).IsEqualTo(new AccountBalance(40m, 40m, Morning));
            await Assert.That(snapshot.IsStale).IsFalse();
        }
    }

    [Test]
    public async Task BalanceSnapshot_Refresh_StaleThenSynced_IsNoLongerStale()
    {
        BankAccount account = AccountWithBalance(40m, Morning.AddDays(-1));
        BalanceSnapshot snapshot = account.SnapshotBalance(Day, synced: false);
        account.UpdateBalance(45m, 45m, Evening);

        snapshot.Refresh(account, synced: true);

        using (Assert.Multiple())
        {
            await Assert.That(snapshot.Balance.Current).IsEqualTo(45m);
            await Assert.That(snapshot.IsStale).IsFalse();
        }
    }

    [Test]
    public async Task BalanceSnapshot_Refresh_AnotherAccount_Throws()
    {
        BalanceSnapshot snapshot = AccountWithBalance(40m, Morning).SnapshotBalance(Day, synced: true);
        BankAccount other = AccountWithBalance(10m, Morning);

        await Assert.That(() => snapshot.Refresh(other, synced: true)).Throws<ArgumentException>();
    }

    [Test]
    public async Task BalanceSnapshot_RequiresAnAccount() =>
        await Assert.That(() => new BalanceSnapshot(UserId, Guid.Empty, Day, new AccountBalance(10m, 20m, Morning),
            BalanceSnapshotSource.Synced, isStale: false)).Throws<ArgumentOutOfRangeException>();

    private static BankAccount AccountWithBalance(decimal current, DateTime asOf)
    {
        BankAccount account = new(UserId, Guid.CreateVersion7(), $"acc-{Guid.NewGuid():N}", "TRANSACTION", "Current",
            "GBP");
        account.UpdateBalance(current, current, asOf);
        return account;
    }
}
