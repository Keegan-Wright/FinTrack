using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Transactions;

[Category("Domain")]
public class BankTransactionTests
{
    private static readonly DateTime OccurredAt = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task BankTransaction_Import_TakesOwnershipFromAccountAndSetsDetails()
    {
        var account = BuildAccount();
        var details = Details("tx-1");

        var transaction = BankTransaction.Import(account, details);

        using (Assert.Multiple())
        {
            await Assert.That(transaction.UserId).IsEqualTo(account.UserId);
            await Assert.That(transaction.AccountId).IsEqualTo(account.Id);
            await Assert.That(transaction.ExternalId).IsEqualTo("tx-1");
            await Assert.That(transaction.Description).IsEqualTo(details.Description);
            await Assert.That(transaction.Type).IsEqualTo(details.Type);
            await Assert.That(transaction.Category).IsEqualTo(details.Category);
            await Assert.That(transaction.Amount).IsEqualTo(details.Amount);
            await Assert.That(transaction.Currency).IsEqualTo(details.Currency);
            await Assert.That(transaction.OccurredAt).IsEqualTo(details.OccurredAt);
            await Assert.That(transaction.IsPending).IsEqualTo(details.IsPending);
            await Assert.That(transaction.ProviderClassifications).IsEquivalentTo(details.ProviderClassifications);
            await Assert.That(transaction.CustomClassificationIds.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task BankTransaction_Import_DuplicateProviderClassifications_AreStoredOnce()
    {
        var transaction = BankTransaction.Import(BuildAccount(),
            Details("tx-1") with { ProviderClassifications = ["Food", "Food", "Bills"] });

        await Assert.That(transaction.ProviderClassifications).IsEquivalentTo(["Food", "Bills"]);
    }

    [Test]
    public void BankTransaction_Import_NullAccount_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BankTransaction.Import(null!, Details("tx-1")));
    }

    [Test]
    public void BankTransaction_Import_NullDetails_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BankTransaction.Import(BuildAccount(), null!));
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public void BankTransaction_Import_EmptyExternalId_Throws_ArgumentException(string externalId)
    {
        Assert.Throws<ArgumentException>(() =>
            BankTransaction.Import(BuildAccount(), Details(externalId)));
    }

    [Test]
    public async Task BankTransaction_UpdateFromSync_ReplacesDetails()
    {
        var transaction = BankTransaction.Import(BuildAccount(), Details("tx-1") with
        {
            IsPending = true, ProviderClassifications = ["Old"]
        });
        var updated = new TransactionDetails("tx-1", "Updated", TransactionType.Credit, "CREDIT", 99m, "EUR",
            OccurredAt.AddDays(1), false, ["New"]);

        transaction.UpdateFromSync(updated);

        using (Assert.Multiple())
        {
            await Assert.That(transaction.Description).IsEqualTo("Updated");
            await Assert.That(transaction.Type).IsEqualTo(TransactionType.Credit);
            await Assert.That(transaction.Category).IsEqualTo("CREDIT");
            await Assert.That(transaction.Amount).IsEqualTo(99m);
            await Assert.That(transaction.Currency).IsEqualTo("EUR");
            await Assert.That(transaction.OccurredAt).IsEqualTo(OccurredAt.AddDays(1));
            await Assert.That(transaction.IsPending).IsFalse();
            await Assert.That(transaction.ProviderClassifications).IsEquivalentTo(["New"]);
        }
    }

    [Test]
    public void BankTransaction_UpdateFromSync_DifferentTransaction_Throws_InvalidOperationException()
    {
        var transaction = BankTransaction.Import(BuildAccount(), Details("tx-1"));

        Assert.Throws<InvalidOperationException>(() =>
            transaction.UpdateFromSync(Details("tx-2")));
    }

    [Test]
    public void BankTransaction_UpdateFromSync_Null_Throws_ArgumentNullException()
    {
        var transaction = BankTransaction.Import(BuildAccount(), Details("tx-1"));

        Assert.Throws<ArgumentNullException>(() =>
            transaction.UpdateFromSync(null!));
    }

    [Test]
    public async Task BankTransaction_UpdateFromSync_KeepsCustomClassifications()
    {
        var account = BuildAccount();
        var transaction = BankTransaction.Import(account, Details("tx-1"));
        var classification = new CustomClassification(account.UserId, "Holiday");
        transaction.Classify(classification);

        transaction.UpdateFromSync(Details("tx-1"));

        await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([classification.Id]);
    }

    [Test]
    public async Task BankTransaction_Classify_AddsClassificationOnce()
    {
        var account = BuildAccount();
        var transaction = BankTransaction.Import(account, Details("tx-1"));
        var classification = new CustomClassification(account.UserId, "Holiday");

        transaction.Classify(classification);
        transaction.Classify(classification);

        await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([classification.Id]);
    }

    [Test]
    public void BankTransaction_Classify_AnotherUsersClassification_Throws_InvalidOperationException()
    {
        var transaction = BankTransaction.Import(BuildAccount(), Details("tx-1"));
        var classification = new CustomClassification(Guid.NewGuid(), "Holiday");

        Assert.Throws<InvalidOperationException>(() =>
            transaction.Classify(classification));
    }

    [Test]
    public void BankTransaction_Classify_Null_Throws_ArgumentNullException()
    {
        var transaction = BankTransaction.Import(BuildAccount(), Details("tx-1"));

        Assert.Throws<ArgumentNullException>(() =>
            transaction.Classify(null!));
    }

    [Test]
    public async Task BankTransaction_Unclassify_RemovesOnlyThatClassification()
    {
        var account = BuildAccount();
        var transaction = BankTransaction.Import(account, Details("tx-1"));
        var holiday = new CustomClassification(account.UserId, "Holiday");
        var food = new CustomClassification(account.UserId, "Food");
        transaction.Classify(holiday);
        transaction.Classify(food);

        transaction.Unclassify(holiday.Id);

        await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([food.Id]);
    }

    [Test]
    public async Task BankTransaction_Unclassify_NotApplied_DoesNothing()
    {
        var transaction = BankTransaction.Import(BuildAccount(), Details("tx-1"));

        transaction.Unclassify(Guid.NewGuid());

        await Assert.That(transaction.CustomClassificationIds.Count).IsEqualTo(0);
    }

    private static BankAccount BuildAccount() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    private static TransactionDetails Details(string externalId) =>
        new(externalId, "Coffee", TransactionType.Debit, "PURCHASE", -3.5m, "GBP", OccurredAt, false, ["Food"]);
}
