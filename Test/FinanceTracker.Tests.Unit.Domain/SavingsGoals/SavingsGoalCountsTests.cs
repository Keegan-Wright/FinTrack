using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.SavingsGoals;

namespace FinanceTracker.Tests.Unit.Domain.SavingsGoals;

[Category("Domain")]
public class SavingsGoalCountsTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Current = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Current", "GBP");
    private static readonly BankAccount Savings = new(UserId, Guid.NewGuid(), "acc-2", "SAVINGS", "Rainy Day", "GBP");
    private static readonly Guid CarFund = Guid.NewGuid();

    [Test]
    public async Task Counts_AccountGoal_EveryTransactionOnItsAccount()
    {
        // Arrange
        SavingsGoal goal = Goal();
        goal.TrackAccount(Savings.Id);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(goal.Counts(Paid(Savings, -20m, new DateTime(2026, 3, 1)), TimeZoneInfo.Utc)).IsTrue();
            await Assert.That(goal.Counts(Paid(Current, -20m, new DateTime(2026, 3, 1)), TimeZoneInfo.Utc)).IsFalse();
        }
    }

    [Test]
    public async Task Counts_TagGoal_TaggedTransactionsFromItsDayOn()
    {
        // Arrange
        SavingsGoal goal = Goal();
        goal.TrackTags([CarFund], 0m, new DateOnly(2026, 2, 1));

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(goal.Counts(Paid(Current, 50m, new DateTime(2026, 2, 1), CarFund), TimeZoneInfo.Utc))
                .IsTrue();
            await Assert.That(goal.Counts(Paid(Current, 50m, new DateTime(2026, 1, 31), CarFund), TimeZoneInfo.Utc))
                .IsFalse();
            await Assert.That(goal.Counts(Paid(Current, 50m, new DateTime(2026, 2, 5)), TimeZoneInfo.Utc)).IsFalse();
        }
    }

    [Test]
    public async Task Counts_TagGoal_UsesTheDayWhereTheUserIs()
    {
        // Arrange, 11pm UTC on the 31st is already the 1st an hour east
        SavingsGoal goal = Goal();
        goal.TrackTags([CarFund], 0m, new DateOnly(2026, 2, 1));
        BankTransaction lateEvening = Paid(Current, 50m, new DateTime(2026, 1, 31, 23, 0, 0), CarFund);
        TimeZoneInfo anHourEast = TimeZoneInfo.CreateCustomTimeZone("east", TimeSpan.FromHours(1), "East", "East");

        // Act & Assert
        await Assert.That(goal.Counts(lateEvening, anHourEast)).IsTrue();
    }

    [Test]
    public async Task Counts_TypedInGoal_Nothing()
    {
        // Arrange
        SavingsGoal goal = Goal();

        // Act & Assert
        await Assert.That(goal.Counts(Paid(Savings, 50m, new DateTime(2026, 2, 1), CarFund), TimeZoneInfo.Utc))
            .IsFalse();
    }

    private static SavingsGoal Goal() => new(UserId, "Car", 0m, 0m, 1000m, null);

    private static BankTransaction Paid(BankAccount account, decimal amount, DateTime occurredAt,
        params Guid[] tagIds)
    {
        BankTransaction transaction = BankTransaction.Import(account, new TransactionDetails(
            Guid.NewGuid().ToString(), "SAVINGS", amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            "TRANSFER", amount, "GBP", DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc), false, [],
            TransactionMetadata.None, false));

        // Tagged as a rule would, which takes the tag ids as they are
        Guid ruleId = Guid.NewGuid();
        transaction.ApplyRules(new RuleOutcome(tagIds.Select(x => new RuleTag(x, ruleId)).ToList(), null),
            DateTime.UtcNow);

        return transaction;
    }
}
