using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Tests.Unit.Domain.Rules;

[Category("Domain")]
public class RuleConditionTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    [Test]
    [Arguments(RuleOperator.Equals, "GROCER STORE 123", true)]
    [Arguments(RuleOperator.Equals, "GROCER", false)]
    [Arguments(RuleOperator.Contains, "STORE", true)]
    [Arguments(RuleOperator.Contains, "CAFE", false)]
    [Arguments(RuleOperator.StartsWith, "GROCER", true)]
    [Arguments(RuleOperator.StartsWith, "STORE", false)]
    [Arguments(RuleOperator.EndsWith, "123", true)]
    [Arguments(RuleOperator.EndsWith, "GROCER", false)]
    [Arguments(RuleOperator.Regex, @"^GROCER\s+STORE\s+\d+$", true)]
    [Arguments(RuleOperator.Regex, @"^\d+", false)]
    public async Task Matches_Description_ComparesText(RuleOperator @operator, string value, bool matches)
    {
        RuleCondition condition = new(RuleField.Description, @operator, value);

        await Assert.That(condition.Matches(Transaction("GROCER STORE 123"))).IsEqualTo(matches);
    }

    [Test]
    [Arguments(RuleOperator.Equals, "grocer store 123")]
    [Arguments(RuleOperator.Contains, "store")]
    [Arguments(RuleOperator.StartsWith, "grocer")]
    [Arguments(RuleOperator.EndsWith, "Store 123")]
    [Arguments(RuleOperator.Regex, "^grocer")]
    public async Task Matches_IgnoreCase_MatchesWhateverTheCase(RuleOperator @operator, string value)
    {
        RuleCondition ignoring = new(RuleField.Description, @operator, value, ignoreCase: true);
        RuleCondition matchingCase = new(RuleField.Description, @operator, value, ignoreCase: false);
        BankTransaction transaction = Transaction("GROCER STORE 123");

        using (Assert.Multiple())
        {
            await Assert.That(ignoring.Matches(transaction)).IsTrue();
            await Assert.That(matchingCase.Matches(transaction)).IsFalse();
        }
    }

    [Test]
    public async Task New_IgnoresCaseByDefault() =>
        await Assert.That(DescriptionContains("store").IgnoreCase).IsTrue();

    [Test]
    public async Task Matches_SlowRegex_TimesOutAsNoMatch()
    {
        // Catastrophic backtracking, far longer than the timeout on a string that almost matches
        RuleCondition condition = new(RuleField.Description, RuleOperator.Regex, "^(a+)+$");

        await Assert.That(condition.Matches(Transaction(new string('a', 40) + "!"))).IsFalse();
    }

    [Test]
    [Arguments("Game Store", true)]
    [Arguments("Someone", false)]
    public async Task Matches_Counterparty_LooksAtMerchantAndCounterpartyNames(string value, bool matches)
    {
        RuleCondition condition = new(RuleField.Counterparty, RuleOperator.Equals, value);
        BankTransaction byMerchant = Transaction(metadata: new TransactionMetadata(null, null, "Game Store",
            null, null));
        BankTransaction byCounterparty = Transaction(metadata: new TransactionMetadata(null, null, null,
            "Game Store", null));

        using (Assert.Multiple())
        {
            await Assert.That(condition.Matches(byMerchant)).IsEqualTo(matches);
            await Assert.That(condition.Matches(byCounterparty)).IsEqualTo(matches);
        }
    }

    [Test]
    public async Task Matches_FieldTheBankDidNotSend_DoesNotMatch()
    {
        RuleCondition condition = new(RuleField.ProviderCategory, RuleOperator.Regex, ".*");

        await Assert.That(condition.Matches(Transaction(metadata: TransactionMetadata.None))).IsFalse();
    }

    [Test]
    public async Task Matches_ProviderCategory_LooksAtTheBanksCode()
    {
        RuleCondition condition = new(RuleField.ProviderCategory, RuleOperator.Equals, "DD");

        await Assert.That(condition.Matches(Transaction(metadata: new TransactionMetadata(null, "DD", null,
            null, null)))).IsTrue();
    }

    [Test]
    public async Task Matches_ProviderClassification_MatchesAnyOfThem()
    {
        RuleCondition condition = new(RuleField.ProviderClassification, RuleOperator.Equals, "Games");

        await Assert.That(condition.Matches(Transaction(providerClassifications: ["Entertainment", "Games"])))
            .IsTrue();
    }

    [Test]
    [Arguments(RuleOperator.Equals, "12.50", null, true)]
    [Arguments(RuleOperator.Equals, "12.49", null, false)]
    [Arguments(RuleOperator.GreaterThan, "12", null, true)]
    [Arguments(RuleOperator.GreaterThan, "12.50", null, false)]
    [Arguments(RuleOperator.LessThan, "13", null, true)]
    [Arguments(RuleOperator.LessThan, "12.50", null, false)]
    [Arguments(RuleOperator.Between, "12.50", "20", true)]
    [Arguments(RuleOperator.Between, "5", "12.50", true)]
    [Arguments(RuleOperator.Between, "13", "20", false)]
    public async Task Matches_Amount_IgnoresTheSign(RuleOperator @operator, string value, string? secondValue,
        bool matches)
    {
        RuleCondition condition = new(RuleField.Amount, @operator, value, secondValue);

        using (Assert.Multiple())
        {
            await Assert.That(condition.Matches(Transaction(amount: -12.5m))).IsEqualTo(matches);
            await Assert.That(condition.Matches(Transaction(amount: 12.5m))).IsEqualTo(matches);
        }
    }

    [Test]
    [Arguments("Debit", true)]
    [Arguments("credit", false)]
    public async Task Matches_Type(string value, bool matches)
    {
        RuleCondition condition = new(RuleField.Type, RuleOperator.Equals, value);

        await Assert.That(condition.Matches(Transaction(amount: -1m))).IsEqualTo(matches);
    }

    [Test]
    public async Task Matches_Account()
    {
        RuleCondition sameAccount = new(RuleField.Account, RuleOperator.Equals, Account.Id.ToString());
        RuleCondition otherAccount = new(RuleField.Account, RuleOperator.Equals, Guid.NewGuid().ToString());
        BankTransaction transaction = Transaction();

        using (Assert.Multiple())
        {
            await Assert.That(sameAccount.Matches(transaction)).IsTrue();
            await Assert.That(otherAccount.Matches(transaction)).IsFalse();
        }
    }

    [Test]
    [Arguments(RuleField.Description, RuleOperator.Between, "a", "b")]
    [Arguments(RuleField.Description, RuleOperator.GreaterThan, "a", null)]
    [Arguments(RuleField.Amount, RuleOperator.Contains, "1", null)]
    [Arguments(RuleField.Type, RuleOperator.Contains, "Debit", null)]
    [Arguments(RuleField.Account, RuleOperator.StartsWith, "abc", null)]
    public void New_OperatorDoesNotSuitField_Throws(RuleField field, RuleOperator @operator, string value,
        string? secondValue) =>
        Assert.Throws<ArgumentException>(() => _ = new RuleCondition(field, @operator, value, secondValue));

    [Test]
    [Arguments(RuleField.Description, RuleOperator.Regex, "(unclosed", null)]
    [Arguments(RuleField.Amount, RuleOperator.Equals, "twelve", null)]
    [Arguments(RuleField.Amount, RuleOperator.Between, "1", null)]
    [Arguments(RuleField.Amount, RuleOperator.Between, "20", "10")]
    [Arguments(RuleField.Amount, RuleOperator.Equals, "1", "2")]
    [Arguments(RuleField.Amount, RuleOperator.Equals, "12,50", null)]
    [Arguments(RuleField.Amount, RuleOperator.Equals, "1,250.00", null)]
    [Arguments(RuleField.Amount, RuleOperator.Equals, "-12.50", null)]
    [Arguments(RuleField.Type, RuleOperator.Equals, "Refund", null)]
    [Arguments(RuleField.Type, RuleOperator.Equals, "1", null)]
    [Arguments(RuleField.Account, RuleOperator.Equals, "main", null)]
    public void New_ValueDoesNotSuit_Throws(RuleField field, RuleOperator @operator, string value,
        string? secondValue) =>
        Assert.Throws<ArgumentException>(() => _ = new RuleCondition(field, @operator, value, secondValue));

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public void New_BlankValue_Throws(string value) =>
        Assert.Throws<ArgumentException>(() =>
            _ = new RuleCondition(RuleField.Description, RuleOperator.Contains, value));

    private static BankTransaction Transaction(string description = "GROCER STORE 123", decimal amount = -12.5m,
        TransactionMetadata? metadata = null, IReadOnlyCollection<string>? providerClassifications = null) =>
        BankTransaction.Import(Account, new TransactionDetails(Guid.NewGuid().ToString(), description,
            amount < 0 ? TransactionType.Debit : TransactionType.Credit, "PURCHASE", amount, "GBP", At, false,
            providerClassifications ?? ["Shopping"], metadata ?? TransactionMetadata.None, false));

    private static RuleCondition DescriptionContains(string value) =>
        new(RuleField.Description, RuleOperator.Contains, value);
}
