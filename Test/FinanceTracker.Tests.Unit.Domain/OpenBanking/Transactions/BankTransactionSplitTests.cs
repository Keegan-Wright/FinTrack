using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.Rules;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Transactions;

[Category("Domain")]
public class BankTransactionSplitTests
{
    private static readonly DateTime OccurredAt = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly BankAccount Account = new(UserId, Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP");

    private readonly CustomClassification _groceries = new(UserId, "Groceries");
    private readonly CustomClassification _household = new(UserId, "Household");

    [Test]
    public async Task Split_KeepsTheParts_AndTheirTagsBecomeTheTransactions()
    {
        // Arrange
        BankTransaction transaction = Build(-100m);
        TransactionSplit[] parts =
        [
            new(-60m, _groceries.Id, "Food"),
            new(-30m, _household.Id, null),
            new(-10m, null, null)
        ];

        // Act
        transaction.Split(parts, [_groceries, _household]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(transaction.IsSplit).IsTrue();
            await Assert.That(transaction.Splits).IsEquivalentTo(parts);
            await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([_groceries.Id, _household.Id]);
            await Assert.That(transaction.TagsSetByUser).IsTrue();
        }
    }

    [Test]
    public async Task Split_ReplacesTheTagsAndRuleTagsBefore()
    {
        // Arrange
        BankTransaction transaction = Build(-100m);
        CustomClassification fromRule = new(UserId, "Shopping");
        transaction.ApplyRules(new RuleOutcome([new RuleTag(fromRule.Id, Guid.NewGuid())], null), OccurredAt);

        // Act
        transaction.Split([new(-50m, _groceries.Id, null), new(-50m, null, null)], [_groceries]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([_groceries.Id]);
            await Assert.That(transaction.RuleTags).IsEmpty();
        }
    }

    [Test]
    public async Task Split_AgainReplacesTheParts()
    {
        // Arrange
        BankTransaction transaction = Build(-100m);
        transaction.Split([new(-50m, _groceries.Id, null), new(-50m, null, null)], [_groceries]);
        TransactionSplit[] parts = [new(-70m, _household.Id, null), new(-30m, null, null)];

        // Act
        transaction.Split(parts, [_household]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(transaction.Splits).IsEquivalentTo(parts);
            await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([_household.Id]);
        }
    }

    [Test]
    public async Task Split_MoneyIn_TakesPartsOfMoneyIn()
    {
        // Arrange
        BankTransaction transaction = Build(40m);

        // Act
        transaction.Split([new(25m, _groceries.Id, null), new(15m, _household.Id, null)], [_groceries, _household]);

        // Assert
        await Assert.That(transaction.Splits.Sum(static x => x.Amount)).IsEqualTo(40m);
    }

    [Test]
    public void Split_PartsThatDontAddUp_Throws()
    {
        BankTransaction transaction = Build(-100m);

        Assert.Throws<ArgumentException>(() =>
            transaction.Split([new(-60m, null, null), new(-30m, null, null)], []));
    }

    [Test]
    [Arguments(-100.5, 0.5)]
    [Arguments(-100, 0)]
    public void Split_APartSignedTheOtherWayOrZero_Throws(double first, double second)
    {
        BankTransaction transaction = Build(-100m);

        Assert.Throws<ArgumentException>(() =>
            transaction.Split([new((decimal)first, null, null), new((decimal)second, null, null)], []));
    }

    [Test]
    public void Split_OnePart_Throws()
    {
        BankTransaction transaction = Build(-100m);

        Assert.Throws<ArgumentException>(() => transaction.Split([new(-100m, null, null)], []));
    }

    [Test]
    public void Split_ATagThatIsntGiven_Throws()
    {
        BankTransaction transaction = Build(-100m);

        Assert.Throws<ArgumentException>(() =>
            transaction.Split([new(-50m, Guid.NewGuid(), null), new(-50m, null, null)], []));
    }

    [Test]
    public void Split_AnotherUsersTag_Throws()
    {
        BankTransaction transaction = Build(-100m);
        CustomClassification theirs = new(Guid.NewGuid(), "Groceries");

        Assert.Throws<InvalidOperationException>(() =>
            transaction.Split([new(-50m, theirs.Id, null), new(-50m, null, null)], [theirs]));
    }

    [Test]
    public void Split_Pending_Throws()
    {
        BankTransaction transaction = BankTransaction.Import(Account, Details(-100m) with { IsPending = true });

        Assert.Throws<InvalidOperationException>(() =>
            transaction.Split([new(-50m, null, null), new(-50m, null, null)], []));
    }

    [Test]
    public async Task Unsplit_KeepsThePartsTagsOnTheWhole()
    {
        // Arrange
        BankTransaction transaction = Build(-100m);
        transaction.Split([new(-60m, _groceries.Id, null), new(-40m, _household.Id, null)], [_groceries, _household]);

        // Act
        bool unsplit = transaction.Unsplit();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(unsplit).IsTrue();
            await Assert.That(transaction.IsSplit).IsFalse();
            await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([_groceries.Id, _household.Id]);
        }
    }

    [Test]
    public async Task Unsplit_NotSplit_DoesNothing()
    {
        BankTransaction transaction = Build(-100m);

        await Assert.That(transaction.Unsplit()).IsFalse();
    }

    [Test]
    public void Classify_Split_Throws()
    {
        BankTransaction transaction = Build(-100m);
        transaction.Split([new(-50m, null, null), new(-50m, null, null)], []);

        Assert.Throws<InvalidOperationException>(() => transaction.Classify(_groceries));
    }

    [Test]
    public async Task RemoveDeletedClassification_LeavesItsPartsUntagged()
    {
        // Arrange
        BankTransaction transaction = Build(-100m);
        transaction.Split([new(-60m, _groceries.Id, "Food"), new(-40m, _household.Id, null)],
            [_groceries, _household]);

        // Act
        transaction.RemoveDeletedClassification(_groceries.Id);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(transaction.Splits)
                .IsEquivalentTo([new TransactionSplit(-60m, null, "Food"), new TransactionSplit(-40m, _household.Id, null)]);
            await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([_household.Id]);
        }
    }

    [Test]
    public async Task UpdateFromSync_TheSameAmount_KeepsTheSplit()
    {
        // Arrange
        BankTransaction transaction = Build(-100m);
        transaction.Split([new(-50m, _groceries.Id, null), new(-50m, null, null)], [_groceries]);

        // Act
        transaction.UpdateFromSync(Details(-100m) with { Description = "Renamed" });

        // Assert
        await Assert.That(transaction.IsSplit).IsTrue();
    }

    [Test]
    public async Task UpdateFromSync_ADifferentAmount_DropsTheSplitButKeepsItsTags()
    {
        // Arrange
        BankTransaction transaction = Build(-100m);
        transaction.Split([new(-50m, _groceries.Id, null), new(-50m, null, null)], [_groceries]);

        // Act
        transaction.UpdateFromSync(Details(-105m));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(transaction.IsSplit).IsFalse();
            await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([_groceries.Id]);
        }
    }

    [Test]
    public async Task ApplyRules_Split_LeavesItsTagsAlone()
    {
        // Arrange
        BankTransaction transaction = Build(-100m);
        transaction.Split([new(-50m, _groceries.Id, null), new(-50m, null, null)], [_groceries]);

        // Act
        transaction.ApplyRules(new RuleOutcome([new RuleTag(_household.Id, Guid.NewGuid())], null), OccurredAt);

        // Assert
        await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([_groceries.Id]);
    }

    private static BankTransaction Build(decimal amount) => BankTransaction.Import(Account, Details(amount));

    private static TransactionDetails Details(decimal amount) =>
        new("tx-1", "Supermarket", TransactionType.Debit, "PURCHASE", amount, "GBP", OccurredAt, false, [],
            TransactionMetadata.None, false);
}
