using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Balances;

[Category("Domain")]
public class BalanceHistoryTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Current = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Current", "GBP");

    private static readonly BankAccount Card = new(UserId, Guid.NewGuid(), "card-1", "CREDIT_CARD", "Card", "GBP",
        BankAccountKind.Card);

    [Test]
    public async Task Backfill_WorksBackFromTheSyncedBalanceToTheDayBeforeTheFirstTransaction()
    {
        // Arrange, 1000 on the 10th after 200 in on the 10th and 50 out on the 8th
        BalanceSnapshot synced = Synced(Current, Day(10), 1000m);
        BankTransaction[] transactions = [Paid(Current, 200m, Day(10)), Paid(Current, -50m, Day(8))];

        // Act
        Dictionary<DateOnly, decimal> derived = Closing(BalanceHistory.Backfill(Current, [synced], transactions,
            TimeZoneInfo.Utc));

        // Assert
        await Assert.That(derived).IsEquivalentTo(new Dictionary<DateOnly, decimal>
        {
            [Day(9)] = 800m,
            [Day(8)] = 800m,
            [Day(7)] = 850m
        });
    }

    [Test]
    public async Task Backfill_AGapBetweenSyncs_IsWorkedBackFromTheLaterOne()
    {
        // Arrange, synced on the 5th and the 8th, nothing between
        BalanceSnapshot fifth = Synced(Current, Day(5), 100m);
        BalanceSnapshot eighth = Synced(Current, Day(8), 400m);
        BankTransaction[] transactions = [Paid(Current, 300m, Day(7)), Paid(Current, 1m, Day(1))];

        // Act
        IReadOnlyList<BalanceSnapshot> derived = BalanceHistory.Backfill(Current, [fifth, eighth], transactions,
            TimeZoneInfo.Utc);

        // Assert, from the 5th back it works from the 5th's own balance
        Dictionary<DateOnly, decimal> closing = Closing(derived);
        using (Assert.Multiple())
        {
            await Assert.That(closing[Day(7)]).IsEqualTo(400m);
            await Assert.That(closing[Day(6)]).IsEqualTo(100m);
            await Assert.That(closing.ContainsKey(Day(5))).IsFalse();
            await Assert.That(closing[Day(4)]).IsEqualTo(100m);
            await Assert.That(closing[Day(0)]).IsEqualTo(99m);
            await Assert.That(derived.All(static x => x.Source == BalanceSnapshotSource.Derived)).IsTrue();
        }
    }

    [Test]
    public async Task Backfill_AStaleSnapshot_IsLeftAloneAndWorkedPast()
    {
        // Arrange, the 9th is stale: a balance from some earlier day
        BalanceSnapshot stale = new(UserId, Current.Id, Day(9), new AccountBalance(5m, 5m, DateTime.UtcNow),
            BalanceSnapshotSource.Synced, isStale: true);
        BalanceSnapshot synced = Synced(Current, Day(10), 1000m);

        // Act
        Dictionary<DateOnly, decimal> closing = Closing(BalanceHistory.Backfill(Current, [stale, synced],
            [Paid(Current, -10m, Day(8))], TimeZoneInfo.Utc));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(closing.ContainsKey(Day(9))).IsFalse();
            await Assert.That(closing[Day(8)]).IsEqualTo(1000m);
            await Assert.That(closing[Day(7)]).IsEqualTo(1010m);
        }
    }

    [Test]
    public async Task Backfill_PendingTransactions_AreLeftOut()
    {
        // Act
        Dictionary<DateOnly, decimal> closing = Closing(BalanceHistory.Backfill(Current,
            [Synced(Current, Day(10), 100m)],
            [Paid(Current, -20m, Day(10), pending: true), Paid(Current, -5m, Day(9))], TimeZoneInfo.Utc));

        // Assert
        await Assert.That(closing[Day(9)]).IsEqualTo(100m);
    }

    [Test]
    public async Task Backfill_ACard_OwedMoreAfterAPurchaseThanBefore()
    {
        // Arrange, owing 39 after spending 30 on the 10th, with 120 of credit
        BalanceSnapshot synced = new(UserId, Card.Id, Day(10), new AccountBalance(81m, -39m, DateTime.UtcNow, 120m),
            BalanceSnapshotSource.Synced, isStale: false);

        // Act
        BalanceSnapshot ninth = BalanceHistory.Backfill(Card, [synced], [Paid(Card, -30m, Day(10))],
            TimeZoneInfo.Utc).Single(x => x.Date == Day(9));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ninth.Balance.Current).IsEqualTo(-9m);
            await Assert.That(ninth.Balance.Available).IsEqualTo(111m);
            await Assert.That(ninth.Balance.CreditLimit).IsEqualTo(120m);
        }
    }

    [Test]
    public async Task Backfill_NothingSyncedYet_DerivesNothing() =>
        await Assert.That(BalanceHistory.Backfill(Current, [], [Paid(Current, 5m, Day(3))], TimeZoneInfo.Utc))
            .IsEmpty();

    [Test]
    public async Task Backfill_AnotherAccountsTransaction_Throws() =>
        await Assert.That(() => BalanceHistory.Backfill(Current, [Synced(Current, Day(10), 1m)],
            [Paid(Card, 5m, Day(3))], TimeZoneInfo.Utc)).Throws<ArgumentException>();

    private static DateOnly Day(int day) => new DateOnly(2026, 9, 30).AddDays(day);

    private static Dictionary<DateOnly, decimal> Closing(IEnumerable<BalanceSnapshot> snapshots) =>
        snapshots.ToDictionary(static x => x.Date, static x => x.Balance.Current);

    private static BalanceSnapshot Synced(BankAccount account, DateOnly day, decimal current) =>
        new(UserId, account.Id, day, new AccountBalance(current, current, DateTime.UtcNow),
            BalanceSnapshotSource.Synced, isStale: false);

    private static BankTransaction Paid(BankAccount account, decimal amount, DateOnly day, bool pending = false) =>
        BankTransaction.Import(account, new TransactionDetails(Guid.NewGuid().ToString(), "PAYMENT",
            amount < 0 ? TransactionType.Debit : TransactionType.Credit, "PURCHASE", amount, "GBP",
            day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), pending, [], TransactionMetadata.None, false));
}
