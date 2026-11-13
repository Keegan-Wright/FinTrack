using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure.Sharing;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Pairing the two copies of a joint account's transactions, each from a different login.
/// </summary>
public class SameTransactionsTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private readonly BankAccount _yours = Account();
    private readonly BankAccount _theirs = Account();

    [Test]
    public async Task SameTransactions_SameDescription_ArePaired()
    {
        BankTransaction from = Transaction(_yours, -20m, Monday, "TESCO STORES");
        BankTransaction to = Transaction(_theirs, -20m, Monday.AddHours(3), "Tesco  stores");

        List<(BankTransaction From, BankTransaction To)> pairs = JointAccounts.SameTransactions([from], [to]);

        await Assert.That(pairs).IsEquivalentTo([(from, to)]);
    }

    [Test]
    public async Task SameTransactions_DescribedDifferently_PairedWhenTheOnlyOneThatDayForThatAmount()
    {
        BankTransaction from = Transaction(_yours, -20m, Monday, "TESCO STORES 3021");
        BankTransaction to = Transaction(_theirs, -20m, Monday, "Card payment to Tesco");

        List<(BankTransaction From, BankTransaction To)> pairs = JointAccounts.SameTransactions([from], [to]);

        await Assert.That(pairs).IsEquivalentTo([(from, to)]);
    }

    [Test]
    public async Task SameTransactions_DescribedDifferently_TwoThatCouldBeIt_LeftApart()
    {
        // Two coffees for the same amount that day, each login naming them its own way, could be either
        BankTransaction first = Transaction(_yours, -3m, Monday, "COSTA 1");
        BankTransaction second = Transaction(_yours, -3m, Monday.AddHours(2), "PRET 2");
        BankTransaction theirFirst = Transaction(_theirs, -3m, Monday, "Costa Coffee");
        BankTransaction theirSecond = Transaction(_theirs, -3m, Monday.AddHours(2), "Pret A Manger");

        List<(BankTransaction From, BankTransaction To)> pairs =
            JointAccounts.SameTransactions([first, second], [theirFirst, theirSecond]);

        await Assert.That(pairs).IsEmpty();
    }

    [Test]
    public async Task SameTransactions_OneMatchesExactly_TheOtherIsLeftAlone_ThenPaired()
    {
        BankTransaction exact = Transaction(_yours, -3m, Monday, "COSTA");
        BankTransaction other = Transaction(_yours, -3m, Monday.AddHours(2), "PRET 2");
        BankTransaction theirExact = Transaction(_theirs, -3m, Monday, "Costa");
        BankTransaction theirOther = Transaction(_theirs, -3m, Monday.AddHours(2), "Pret A Manger");

        List<(BankTransaction From, BankTransaction To)> pairs =
            JointAccounts.SameTransactions([exact, other], [theirExact, theirOther]);

        await Assert.That(pairs).IsEquivalentTo([(exact, theirExact), (other, theirOther)]);
    }

    private static BankAccount Account() =>
        new(Guid.NewGuid(), Guid.NewGuid(), $"ext-{Guid.NewGuid()}", "TRANSACTION", "Joint", "GBP");

    private static BankTransaction Transaction(BankAccount account, decimal amount, DateTime occurredAt,
        string description) =>
        BankTransaction.Import(account, new TransactionDetails($"tx-{Guid.NewGuid()}", description,
            TransactionType.Debit, "PURCHASE", amount, "GBP", occurredAt, false, [], TransactionMetadata.None, false));
}
