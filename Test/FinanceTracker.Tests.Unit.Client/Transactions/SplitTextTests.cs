using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class SplitTextTests
{
    [Test]
    [Arguments(false, false, "Split")]
    [Arguments(false, true, "Edit split")]
    [Arguments(true, false, "Split once it's settled")]
    public async Task ActionLabel_SaysWhatSplittingWillDo(bool pending, bool split, string expected)
    {
        // Arrange
        Transaction transaction = TransactionListTests.Transaction("Supermarket", -100m, pending,
            splits: split ? [new TransactionSplitPart { Amount = -100m }] : []);

        // Act & Assert
        await Assert.That(SplitText.ActionLabel(transaction)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(40, "£60.00 left to split")]
    [Arguments(100, "The parts add up")]
    [Arguments(105, "£5.00 more than the transaction")]
    public async Task RemainingText_SaysWhatsLeft(double entered, string expected)
    {
        // Arrange
        using CultureScope culture = new();
        Transaction transaction = TransactionListTests.Transaction("Supermarket", -100m);

        // Act
        decimal remaining = SplitText.Remaining(transaction, [new SplitPartDraft { Amount = (decimal)entered }]);

        // Assert
        await Assert.That(SplitText.RemainingText(remaining)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(60, 40, true)]
    [Arguments(60, 30, false)]
    [Arguments(100, 0, false)]
    public async Task CanSave_OnlyWhenEveryPartHasMoneyAndTheyAddUp(double first, double second, bool expected)
    {
        // Arrange
        Transaction transaction = TransactionListTests.Transaction("Supermarket", -100m);
        SplitPartDraft[] parts =
        [
            new() { Amount = (decimal)first },
            new() { Amount = (decimal)second }
        ];

        // Act & Assert
        await Assert.That(SplitText.CanSave(transaction, parts)).IsEqualTo(expected);
    }

    [Test]
    public async Task CanSave_OnePart_IsFalse()
    {
        Transaction transaction = TransactionListTests.Transaction("Supermarket", -100m);

        await Assert.That(SplitText.CanSave(transaction, [new SplitPartDraft { Amount = 100m }])).IsFalse();
    }

    [Test]
    [Arguments(-100, -60)]
    [Arguments(100, 60)]
    public async Task Request_SignsThePartsLikeTheTransaction(double amount, double expected)
    {
        // Arrange
        Transaction transaction = TransactionListTests.Transaction("Supermarket", (decimal)amount);
        Guid tagId = Guid.NewGuid();

        // Act
        SplitTransaction request = SplitText.Request(transaction,
            [new SplitPartDraft { Amount = 60m, TagId = tagId, Note = "Food" }, new SplitPartDraft { Amount = 40m }]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(request.Parts[0].Amount).IsEqualTo((decimal)expected);
            await Assert.That(request.Parts[0].TagId).IsEqualTo(tagId);
            await Assert.That(request.Parts[0].Note).IsEqualTo("Food");
            await Assert.That(request.Parts[1].TagId).IsNull();
        }
    }

    [Test]
    public async Task Drafts_AlreadySplit_StartsFromItsParts()
    {
        // Arrange
        Guid tagId = Guid.NewGuid();
        Transaction transaction = TransactionListTests.Transaction("Supermarket", -100m, splits:
        [
            new TransactionSplitPart { Amount = -60m, TagId = tagId, Tag = "Groceries" },
            new TransactionSplitPart { Amount = -40m }
        ]);

        // Act
        List<SplitPartDraft> drafts = SplitText.Drafts(transaction);

        // Assert
        await Assert.That(drafts.Select(static x => (x.Amount, x.TagId)))
            .IsEquivalentTo([(60m, tagId), (40m, Guid.Empty)]);
    }

    [Test]
    public async Task Summary_ListsEachPart()
    {
        // Arrange
        using CultureScope culture = new();
        Transaction transaction = TransactionListTests.Transaction("Supermarket", -100m, splits:
        [
            new TransactionSplitPart { Amount = -60m, Tag = "Groceries" },
            new TransactionSplitPart { Amount = -40m }
        ]);

        // Act & Assert
        await Assert.That(SplitText.Summary(transaction)).IsEqualTo("Split into £60.00 Groceries, £40.00 untagged");
    }
}
