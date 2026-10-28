using Bunit;
using FinanceTracker.Client.Components.Review;
using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Tests.Unit.Client.Review;

public class ReviewPairRowTests : ComponentTestBase
{
    [Test]
    public async Task Render_APair_ShowsBothHalvesAndWhy()
    {
        // Arrange
        using CultureScope culture = new();
        ReviewPair pair = new(Transaction("To savings", "Current", -20m), Transaction("From current", "Savings", 20m));

        // Act
        IRenderedComponent<ReviewPairRow> cut = Render<ReviewPairRow>(p => p.Add(x => x.Pair, pair));

        // Assert
        List<string> halves = cut.FindAll(".ft-review__half").Select(static x => x.TextContent).ToList();

        using (Assert.Multiple())
        {
            await Assert.That(halves).Count().IsEqualTo(2);
            await Assert.That(halves[0]).Contains("To savings");
            await Assert.That(halves[0]).Contains("Current · 4 Oct 2026");
            await Assert.That(halves[1]).Contains("From current");
            await Assert.That(cut.Find(".ft-review").TextContent).Contains("the bank didn't call it a transfer");
        }
    }

    [Test]
    public async Task Render_NoOtherHalf_SaysSo()
    {
        // Arrange
        ReviewPair pair = new(Transaction("Paid someone", "Current", -20m), null);

        // Act
        IRenderedComponent<ReviewPairRow> cut = Render<ReviewPairRow>(p => p.Add(x => x.Pair, pair));

        // Assert
        await Assert.That(cut.Find(".ft-review__half--missing").TextContent)
            .Contains("The other half isn't on any of your accounts");
    }

    [Test]
    [Arguments("It's a transfer", TransferAction.MarkAsTransfer)]
    [Arguments("Not a transfer", TransferAction.NotATransfer)]
    public async Task Choose_RaisesTheDecision(string button, TransferAction expected)
    {
        // Arrange
        TransferAction? chosen = null;
        IRenderedComponent<ReviewPairRow> cut = Render<ReviewPairRow>(p => p
            .Add(x => x.Pair, new ReviewPair(Transaction("To savings", "Current", -20m), null))
            .Add(x => x.OnDecide, (TransferAction action) => chosen = action));

        // Act
        cut.FindAll("button").Single(x => x.TextContent.Trim() == button).Click();

        // Assert
        await Assert.That(chosen).IsEqualTo(expected);
    }

    private static Transaction Transaction(string description, string account, decimal amount) => new()
    {
        TransactionId = Guid.NewGuid(),
        Description = description,
        TransactionType = amount < 0 ? "Debit" : "Credit",
        TransactionCategory = "TRANSFER",
        Amount = amount,
        Currency = "GBP",
        TransactionTime = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc),
        Pending = false,
        Tags = [],
        AccountName = account,
        Transfer = new TransactionTransfer
        {
            State = TransferState.NeedsReview,
            DecidedBy = TransferDecidedBy.FinTrack,
            Reason = TransferMatchReason.MirrorPair
        }
    };
}
