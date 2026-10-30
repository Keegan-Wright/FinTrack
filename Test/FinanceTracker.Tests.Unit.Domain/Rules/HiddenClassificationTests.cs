using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Tests.Unit.Domain.Rules;

/// <summary>
/// Hiding the bank's classifications, by hand and by rule, e.g. <c>TV</c> on a mortgage payment.
/// </summary>
[Category("Domain")]
public class HiddenClassificationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    [Test]
    public async Task HideClassification_ByHand_HidesItFromWhatsShown()
    {
        // Arrange
        BankTransaction transaction = Transaction();

        // Act
        bool hidden = transaction.HideClassification("tv");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(hidden).IsTrue();
            await Assert.That(transaction.VisibleProviderClassifications).IsEquivalentTo(["Bills"]);
            await Assert.That(transaction.ProviderClassifications).IsEquivalentTo(["Bills", "TV"]);
            await Assert.That(transaction.HiddenClassifications.Single())
                .IsEqualTo(new HiddenClassification("TV", null));
        }
    }

    [Test]
    public void HideClassification_TheBankDidntSend_Throws() =>
        Assert.Throws<ArgumentException>(() => Transaction().HideClassification("Groceries"));

    [Test]
    public async Task ShowClassification_HiddenByHand_ShowsItAgain()
    {
        // Arrange
        BankTransaction transaction = Transaction();
        transaction.HideClassification("TV");

        // Act
        bool shown = transaction.ShowClassification("TV");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(shown).IsTrue();
            await Assert.That(transaction.VisibleProviderClassifications).IsEquivalentTo(["Bills", "TV"]);
        }
    }

    [Test]
    public async Task ApplyRules_HidesWhatTheRuleHidesThatTheBankSent()
    {
        // Arrange
        Rule rule = HidesTv();
        BankTransaction transaction = Transaction();

        // Act
        bool changed = RuleEvaluator.Apply([rule, HidesOther()], transaction, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(transaction.HiddenClassifications.Single())
                .IsEqualTo(new HiddenClassification("TV", rule.Id));
            await Assert.That(transaction.WasDecidedBy(rule.Id)).IsTrue();
        }
    }

    [Test]
    public async Task ApplyRules_RuleNoLongerHidesIt_ShowsItAgain()
    {
        // Arrange
        BankTransaction transaction = Transaction();
        RuleEvaluator.Apply([HidesTv()], transaction, At);

        // Act
        bool changed = RuleEvaluator.Apply([], transaction, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(transaction.VisibleProviderClassifications).IsEquivalentTo(["Bills", "TV"]);
        }
    }

    [Test]
    public async Task ApplyRules_HiddenByHand_StaysTheUsers()
    {
        // Arrange
        BankTransaction transaction = Transaction();
        transaction.HideClassification("TV");

        // Act
        bool changed = RuleEvaluator.Apply([HidesTv()], transaction, At);
        RuleEvaluator.Apply([], transaction, At);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(transaction.HiddenClassifications.Single().HiddenByUser).IsTrue();
        }
    }

    [Test]
    public async Task ShowClassification_HiddenByARule_StaysHidden()
    {
        // Arrange
        BankTransaction transaction = Transaction();
        RuleEvaluator.Apply([HidesTv()], transaction, At);

        // Act
        bool shown = transaction.ShowClassification("TV");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(shown).IsFalse();
            await Assert.That(transaction.VisibleProviderClassifications).IsEquivalentTo(["Bills"]);
        }
    }

    [Test]
    public void ReplaceActions_HideWithoutAClassification_Throws() =>
        Assert.Throws<ArgumentException>(() => _ = new Rule(UserId, "Mortgage", 0, RuleMatchMode.All,
            [MortgageCondition()], [new RuleAction(RuleActionType.HideClassification, null)]));

    private static Rule HidesTv() =>
        new(UserId, "Mortgage", 0, RuleMatchMode.All, [MortgageCondition()], [RuleAction.HideClassification("tv")]);

    private static Rule HidesOther() =>
        new(UserId, "Other", 1, RuleMatchMode.All, [MortgageCondition()], [RuleAction.HideClassification("Games")]);

    private static RuleCondition MortgageCondition() => new(RuleField.Description, RuleOperator.Contains, "MORTGAGE");

    private static BankTransaction Transaction() =>
        BankTransaction.Import(Account, new TransactionDetails(Guid.NewGuid().ToString(), "MORTGAGE PAYMENT",
            TransactionType.Debit, "DIRECT_DEBIT", -950m, "GBP", At, false, ["Bills", "TV"],
            TransactionMetadata.None, false));
}
