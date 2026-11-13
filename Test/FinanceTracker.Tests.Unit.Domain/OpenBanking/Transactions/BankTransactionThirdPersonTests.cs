using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Transactions;

/// <summary>
/// Sam's transaction, which Sam lets Alex change. Alex may put on it their own things or Sam's, never Chris's, which
/// Chris shares only with Alex.
/// </summary>
[Category("Domain")]
public class BankTransactionThirdPersonTests
{
    private static readonly DateTime At = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Alex = Guid.NewGuid();
    private static readonly Guid Sam = Guid.NewGuid();
    private static readonly Guid Chris = Guid.NewGuid();
    private static readonly BankAccount SamsAccount = new(Sam, Guid.NewGuid(), "acc-1", "TRANSACTION", "Joint", "GBP");

    [Test]
    [Arguments("alex", true)]
    [Arguments("sam", true)]
    [Arguments("chris", false)]
    public async Task PayBill_OnAnotherPersonsTransaction_OnlyTheirsOrTheOwners(string owner, bool allowed)
    {
        BankTransaction transaction = Build(SamsAccount);
        Bill bill = Bill(UserOf(owner));

        await AssertAllowedAsync(() => transaction.PayBill(bill, Alex, At, sharedWithThem: owner != "alex"), allowed);
    }

    [Test]
    [Arguments("alex", true)]
    [Arguments("sam", true)]
    [Arguments("chris", false)]
    public async Task PayDebt_OnAnotherPersonsTransaction_OnlyTheirsOrTheOwners(string owner, bool allowed)
    {
        BankTransaction transaction = Build(SamsAccount);
        Debt debt = new(UserOf(owner), "Car loan", null, 5_000m, new DateOnly(2026, 1, 1), 5m, 200m);

        await AssertAllowedAsync(() => transaction.PayDebt(debt, Alex, At, sharedWithThem: owner != "alex"), allowed);
    }

    [Test]
    [Arguments("alex", true)]
    [Arguments("sam", true)]
    [Arguments("chris", false)]
    public async Task Classify_OnAnotherPersonsTransaction_OnlyTheirsOrTheOwners(string owner, bool allowed)
    {
        BankTransaction transaction = Build(SamsAccount);
        CustomClassification tag = new(UserOf(owner), "Groceries");

        await AssertAllowedAsync(() => transaction.Classify(tag, Alex, [tag.Id]), allowed);
    }

    [Test]
    [Arguments("alex", true)]
    [Arguments("sam", true)]
    [Arguments("chris", false)]
    public async Task Split_OnAnotherPersonsTransaction_OnlyTheirsOrTheOwners(string owner, bool allowed)
    {
        BankTransaction transaction = Build(SamsAccount);
        CustomClassification tag = new(UserOf(owner), "Groceries");

        await AssertAllowedAsync(() => transaction.Split(
            [new TransactionSplit(-5m, tag.Id, null), new TransactionSplit(-5m, null, null)], [tag], Alex, [tag.Id]),
            allowed);
    }

    [Test]
    public async Task PayBill_OnTheirOwnTransaction_AnyoneElsesSharedWithThem()
    {
        // Their own to tell Chris of, by paying Chris's bill with it
        BankTransaction transaction = Build(new BankAccount(Alex, Guid.NewGuid(), "acc-2", "TRANSACTION", "Current",
            "GBP"));

        transaction.PayBill(Bill(Chris), Alex, At, sharedWithThem: true);

        await Assert.That(transaction.BillPayment?.BillId).IsNotNull();
    }

    private static async Task AssertAllowedAsync(Action act, bool allowed)
    {
        if (allowed)
        {
            await Assert.That(act).ThrowsNothing();
            return;
        }

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    private static Guid UserOf(string name) => name switch
    {
        "alex" => Alex,
        "sam" => Sam,
        _ => Chris
    };

    private static Bill Bill(Guid userId) =>
        new(userId, SamsAccount.Id, "Rent", "LANDLORD", BillFrequency.Monthly, 10m, new DateOnly(2026, 1, 1),
            BillSource.Manual);

    private static BankTransaction Build(BankAccount account) =>
        BankTransaction.Import(account, new TransactionDetails($"tx-{Guid.NewGuid()}", "LANDLORD",
            TransactionType.Debit, "PURCHASE", -10m, "GBP", At, false, [], TransactionMetadata.None, false));
}
