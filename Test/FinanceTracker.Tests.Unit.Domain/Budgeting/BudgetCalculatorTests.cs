using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Tests.Unit.Domain.Budgeting;

[Category("Domain")]
public class BudgetCalculatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");
    private static readonly Guid Shopping = Guid.NewGuid();
    private static readonly Guid Gaming = Guid.NewGuid();
    private static readonly BudgetMonth January = new(2026, 1);
    private static readonly BudgetMonth February = new(2026, 2);
    private static readonly BudgetMonth March = new(2026, 3);
    private static readonly DateOnly MidMarch = new(2026, 3, 15);

    [Test]
    public async Task ForMonth_SpentIsMoneyOutLessRefunds()
    {
        // Arrange
        Budget budget = Budget(RolloverMode.None);
        BankTransaction[] transactions =
        [
            Tagged(-30m, new DateTime(2026, 3, 2), Shopping),
            Tagged(-10m, new DateTime(2026, 3, 5), Shopping),
            Tagged(10m, new DateTime(2026, 3, 9), Shopping)
        ];

        // Act
        BudgetMonthResult result = Calculate(budget, transactions, March);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Spent).IsEqualTo(30m);
            await Assert.That(result.Remaining).IsEqualTo(70m);
        }
    }

    [Test]
    public async Task ForMonth_MoreInThanOut_StopsAtNothingSpent()
    {
        // Arrange, money in carrying the tag outweighs what went out
        Budget budget = Budget(RolloverMode.CarryUnderspend);
        BankTransaction[] transactions =
        [
            Tagged(-30m, new DateTime(2026, 3, 2), Shopping),
            Tagged(1200m, new DateTime(2026, 3, 9), Shopping)
        ];

        // Act
        BudgetMonthResult result = Calculate(budget, transactions, March);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Spent).IsEqualTo(0m);
            await Assert.That(result.Remaining).IsEqualTo(100m);
        }
    }

    [Test]
    public async Task ForMonth_PendingCountsAndIsShownSeparately()
    {
        // Arrange
        Budget budget = Budget(RolloverMode.None);
        BankTransaction[] transactions =
        [
            Tagged(-30m, new DateTime(2026, 3, 2), Shopping),
            Pending(-12m, new DateTime(2026, 3, 14), Shopping)
        ];

        // Act
        BudgetMonthResult result = Calculate(budget, transactions, March);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Spent).IsEqualTo(42m);
            await Assert.That(result.Pending).IsEqualTo(12m);
        }
    }

    [Test]
    public async Task ForMonth_TransfersAndOtherTagsAndMonths_DontCount()
    {
        // Arrange
        Budget budget = Budget(RolloverMode.None);
        BankTransaction transfer = Tagged(-50m, new DateTime(2026, 3, 3), Shopping);
        transfer.DecideTransfer(TransferInfo.FromRule(TransferDecision.Transfer, Guid.NewGuid(), DateTime.UtcNow));
        BankTransaction[] transactions =
        [
            transfer,
            Tagged(-20m, new DateTime(2026, 3, 4), Gaming),
            Tagged(-15m, new DateTime(2026, 2, 27), Shopping)
        ];

        // Act
        BudgetMonthResult result = Calculate(budget, transactions, March);

        // Assert
        await Assert.That(result.Spent).IsEqualTo(0m);
    }

    [Test]
    [Arguments(RolloverMode.None, 0)]
    [Arguments(RolloverMode.CarryUnderspend, 40)]
    [Arguments(RolloverMode.CarryBoth, 40)]
    public async Task ForMonth_Underspent_CarriesByTheMode(RolloverMode mode, int carried)
    {
        // Arrange, £60 of £100 spent in February
        Budget budget = Budget(mode, from: February);

        // Act
        BudgetMonthResult result = Calculate(budget, [Tagged(-60m, new DateTime(2026, 2, 10), Shopping)], March);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.CarriedIn).IsEqualTo((decimal)carried);
            await Assert.That(result.Available).IsEqualTo(100m + carried);
        }
    }

    [Test]
    [Arguments(RolloverMode.None, 0)]
    [Arguments(RolloverMode.CarryUnderspend, 0)]
    [Arguments(RolloverMode.CarryBoth, -30)]
    public async Task ForMonth_Overspent_CarriesByTheMode(RolloverMode mode, int carried)
    {
        // Arrange, £130 of £100 spent in February
        Budget budget = Budget(mode, from: February);

        // Act
        BudgetMonthResult result = Calculate(budget, [Tagged(-130m, new DateTime(2026, 2, 10), Shopping)], March);

        // Assert
        await Assert.That(result.CarriedIn).IsEqualTo((decimal)carried);
    }

    [Test]
    public async Task ForMonth_RolloverRunsThroughEveryMonthWithItsOwnLimit()
    {
        // Arrange, nothing spent: £100 in January, £50 from February
        Budget budget = Budget(RolloverMode.CarryUnderspend, from: January);
        budget.ChangeLimit(50m, February);

        // Act
        BudgetMonthResult result = Calculate(budget, [], March);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Limit).IsEqualTo(50m);
            await Assert.That(result.CarriedIn).IsEqualTo(150m);
        }
    }

    [Test]
    public async Task ForMonth_MidMonth_ExpectsAStraightLine()
    {
        // Arrange, £60 spent by the 15th of 31 days
        Budget budget = Budget(RolloverMode.None);

        // Act
        BudgetMonthResult result = Calculate(budget, [Tagged(-60m, new DateTime(2026, 3, 10), Shopping)], March);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.ExpectedByNow).IsEqualTo(48.39m);
            await Assert.That(result.OnPace).IsFalse();
        }
    }

    [Test]
    public async Task ForMonth_PastMonth_ExpectsAllOfIt() =>
        await Assert.That(Calculate(Budget(RolloverMode.None, from: February), [], February).ExpectedByNow)
            .IsEqualTo(100m);

    [Test]
    public async Task ForMonth_BeforeTheBudgetStarted_IsNull() =>
        await Assert.That(BudgetCalculator.ForMonth(Budget(RolloverMode.None), [], February, TimeZoneInfo.Utc,
            MidMarch)).IsNull();

    [Test]
    public async Task ForMonth_InEveryBudgetItsTagsAreIn()
    {
        // Arrange
        Budget shopping = Budget(RolloverMode.None);
        Budget gaming = new(UserId, "Gaming", [Gaming], 50m, March, RolloverMode.None);
        BankTransaction[] transactions = [Tagged(-20m, new DateTime(2026, 3, 4), Shopping, Gaming)];

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(Calculate(shopping, transactions, March).Spent).IsEqualTo(20m);
            await Assert.That(Calculate(gaming, transactions, March).Spent).IsEqualTo(20m);
        }
    }

    [Test]
    public async Task Unbudgeted_MoneyOutNoBudgetCovers()
    {
        // Arrange
        Budget budget = Budget(RolloverMode.None);
        BankTransaction[] transactions =
        [
            Tagged(-30m, new DateTime(2026, 3, 2), Shopping),
            Tagged(-25m, new DateTime(2026, 3, 3)),
            Tagged(-5m, new DateTime(2026, 3, 4), Gaming),
            Income(2000m, new DateTime(2026, 3, 25))
        ];

        // Act
        decimal unbudgeted = BudgetCalculator.Unbudgeted([budget], transactions, March, TimeZoneInfo.Utc);

        // Assert, the salary isn't a refund so it doesn't hide the spending
        await Assert.That(unbudgeted).IsEqualTo(30m);
    }

    [Test]
    public async Task Unbudgeted_RefundsReduceIt_DownToNothing()
    {
        // Arrange, a card refund comes back as a purchase
        BankTransaction[] partlyRefunded =
            [Tagged(-25m, new DateTime(2026, 3, 3)), Tagged(10m, new DateTime(2026, 3, 9))];
        BankTransaction[] overRefunded =
            [Tagged(-5m, new DateTime(2026, 3, 3)), Tagged(40m, new DateTime(2026, 3, 9))];

        // Act
        decimal partly = BudgetCalculator.Unbudgeted([], partlyRefunded, March, TimeZoneInfo.Utc);
        decimal over = BudgetCalculator.Unbudgeted([], overRefunded, March, TimeZoneInfo.Utc);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(partly).IsEqualTo(15m);
            await Assert.That(over).IsEqualTo(0m);
        }
    }

    private static BudgetMonthResult Calculate(Budget budget, IEnumerable<BankTransaction> transactions,
        BudgetMonth month) => BudgetCalculator.ForMonth(budget, transactions, month, TimeZoneInfo.Utc, MidMarch)!;

    private static Budget Budget(RolloverMode mode, BudgetMonth? from = null) =>
        new(UserId, "Shopping", [Shopping], 100m, from ?? March, mode);

    /// <summary>
    /// A card purchase, or a refund of one when <paramref name="amount"/> is money in.
    /// </summary>
    private static BankTransaction Tagged(decimal amount, DateTime occurredAt, params Guid[] tagIds) =>
        Import(amount, occurredAt, false, "PURCHASE", tagIds);

    private static BankTransaction Pending(decimal amount, DateTime occurredAt, params Guid[] tagIds) =>
        Import(amount, occurredAt, true, "PURCHASE", tagIds);

    /// <summary>
    /// Money in the bank doesn't call a purchase, e.g. salary.
    /// </summary>
    private static BankTransaction Income(decimal amount, DateTime occurredAt) =>
        Import(amount, occurredAt, false, "CREDIT", []);

    private static BankTransaction Import(decimal amount, DateTime occurredAt, bool pending, string category,
        Guid[] tagIds)
    {
        BankTransaction transaction = BankTransaction.Import(Account, new TransactionDetails(
            Guid.NewGuid().ToString(), "PURCHASE", amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            category, amount, "GBP", DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc), pending, [],
            TransactionMetadata.None, false));

        // Tagged as a rule would, which takes the tag ids as they are
        Guid ruleId = Guid.NewGuid();
        transaction.ApplyRules(new RuleOutcome(tagIds.Select(x => new RuleTag(x, ruleId)).ToList(), null),
            DateTime.UtcNow);

        return transaction;
    }
}
