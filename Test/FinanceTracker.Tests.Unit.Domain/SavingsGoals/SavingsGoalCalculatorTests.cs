using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.SavingsGoals;

namespace FinanceTracker.Tests.Unit.Domain.SavingsGoals;

[Category("Domain")]
public class SavingsGoalCalculatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Current = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Current", "GBP");
    private static readonly BankAccount Savings = new(UserId, Guid.NewGuid(), "acc-2", "SAVINGS", "Rainy Day", "GBP");
    private static readonly Guid CarFund = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 3, 15);

    [Test]
    public async Task Progress_Manual_IsWhatWasTypedIn()
    {
        // Arrange
        SavingsGoal goal = Goal(saved: 250m);

        // Act
        SavingsGoalProgress progress = Calculate(goal, accountBalance: 999m);

        // Assert
        await Assert.That(progress.Saved).IsEqualTo(250m);
    }

    [Test]
    public async Task Progress_Account_IsItsBalance()
    {
        // Arrange
        SavingsGoal goal = Goal();
        goal.TrackAccount(Savings.Id);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(Calculate(goal, accountBalance: 1234.56m).Saved).IsEqualTo(1234.56m);
            await Assert.That(Calculate(goal, accountBalance: null).Saved).IsEqualTo(0m);
        }
    }

    [Test]
    public async Task Progress_Tags_IsTheStartPlusMoneyInLessMoneyOutFromTheDay()
    {
        // Arrange
        SavingsGoal goal = Goal();
        goal.TrackTags([CarFund], 100m, new DateOnly(2026, 2, 1));
        BankTransaction[] transactions =
        [
            Tagged(Savings, 200m, new DateTime(2026, 2, 5), CarFund),
            Tagged(Savings, -50m, new DateTime(2026, 3, 1), CarFund),
            Tagged(Savings, 999m, new DateTime(2026, 1, 20), CarFund),
            Tagged(Savings, 500m, new DateTime(2026, 2, 6))
        ];

        // Act
        SavingsGoalProgress progress = Calculate(goal, transactions: transactions);

        // Assert
        await Assert.That(progress.Saved).IsEqualTo(250m);
    }

    [Test]
    public async Task Progress_Tags_BothHalvesOfATransferTagged_CountsTheMoneyInOnce()
    {
        // Arrange
        SavingsGoal goal = Goal();
        goal.TrackTags([CarFund], 0m, new DateOnly(2026, 1, 1));
        BankTransaction outgoing = Tagged(Current, -100m, new DateTime(2026, 3, 2), CarFund);
        BankTransaction incoming = Tagged(Savings, 100m, new DateTime(2026, 3, 2), CarFund);
        DateTime decidedAt = DateTime.UtcNow;
        outgoing.DecideTransfer(TransferInfo.Matched(TransferReason.MirrorPair, incoming.Id, Savings.Id, decidedAt));
        incoming.DecideTransfer(TransferInfo.Matched(TransferReason.MirrorPair, outgoing.Id, Current.Id, decidedAt));

        // Act
        SavingsGoalProgress progress = Calculate(goal, transactions: [outgoing, incoming]);

        // Assert
        await Assert.That(progress.Saved).IsEqualTo(100m);
    }

    [Test]
    public async Task Progress_Tags_OnlyTheOutgoingHalfTagged_CountsAgainstTheGoal()
    {
        // Arrange, the rule tags money leaving savings, a withdrawal
        SavingsGoal goal = Goal();
        goal.TrackTags([CarFund], 300m, new DateOnly(2026, 1, 1));

        // Act
        SavingsGoalProgress progress = Calculate(goal,
            transactions: [Tagged(Savings, -100m, new DateTime(2026, 3, 2), CarFund)]);

        // Assert
        await Assert.That(progress.Saved).IsEqualTo(200m);
    }

    [Test]
    public async Task Progress_ContributionReachesTheTargetInTime_IsOnTrack()
    {
        // Arrange, £1,000 to go at £250 a month, four months to June
        SavingsGoal goal = Goal(saved: 9000m, monthly: 250m, target: 10000m, by: new DateTime(2026, 7, 1));

        // Act
        SavingsGoalProgress progress = Calculate(goal);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(progress.OnTrack).IsTrue();
            await Assert.That(progress.ReachedBy).IsEqualTo(new DateOnly(2026, 7, 1));
            await Assert.That(progress.NeededPerMonth).IsEqualTo(250m);
        }
    }

    [Test]
    public async Task Progress_ContributionTooSmall_SaysWhatsNeeded()
    {
        // Arrange, £1,000 to go at £100 a month, four months
        SavingsGoal goal = Goal(saved: 9000m, monthly: 100m, target: 10000m, by: new DateTime(2026, 7, 1));

        // Act
        SavingsGoalProgress progress = Calculate(goal);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(progress.OnTrack).IsFalse();
            await Assert.That(progress.NeededPerMonth).IsEqualTo(250m);
            await Assert.That(progress.ReachedBy).IsEqualTo(new DateOnly(2027, 1, 1));
        }
    }

    [Test]
    public async Task Progress_NoTargetOrContribution_JudgesNothing()
    {
        // Act
        SavingsGoalProgress progress = Calculate(Goal(saved: 100m));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(progress.OnTrack).IsNull();
            await Assert.That(progress.ReachedBy).IsNull();
            await Assert.That(progress.NeededPerMonth).IsNull();
        }
    }

    [Test]
    public async Task Progress_TargetReached_IsOnTrackWithNothingToGo()
    {
        // Act
        SavingsGoalProgress progress = Calculate(Goal(saved: 500m, monthly: 50m, target: 400m));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(progress.OnTrack).IsTrue();
            await Assert.That(progress.ReachedBy).IsNull();
        }
    }

    private static SavingsGoalProgress Calculate(SavingsGoal goal, decimal? accountBalance = null,
        IEnumerable<BankTransaction>? transactions = null) =>
        SavingsGoalCalculator.Progress(goal, accountBalance, transactions ?? [], TimeZoneInfo.Utc, Today);

    private static SavingsGoal Goal(decimal saved = 0m, decimal monthly = 0m, decimal target = 0m,
        DateTime? by = null) => new(UserId, "Car", saved, monthly, target, by);

    private static BankTransaction Tagged(BankAccount account, decimal amount, DateTime occurredAt,
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
