using Bunit;
using FinanceTracker.Client.Components.Review;
using FinanceTracker.Contracts.Bills;

namespace FinanceTracker.Tests.Unit.Client.Review;

public class SuggestionRowTests : ComponentTestBase
{
    [Test]
    public async Task Render_ShowsHowOftenAndThePaymentsBehindIt()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<SuggestionRow> cut = Render<SuggestionRow>(p => p
            .Add(x => x.Suggestion, Suggestion())
            .Add(x => x.Today, new DateOnly(2026, 10, 30)));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Card · Monthly · next due 12 Nov");
            await Assert.That(cut.FindAll(".ft-suggestion__payment")).Count().IsEqualTo(2);
            await Assert.That(cut.Markup).Contains("12 Oct 2026 · STREAMCO 222");
        }
    }

    [Test]
    [Arguments("It's a bill", true)]
    [Arguments("Not a bill", false)]
    public async Task Decide_RaisesTheChoice(string button, bool isBill)
    {
        // Arrange
        bool? chosen = null;
        IRenderedComponent<SuggestionRow> cut = Render<SuggestionRow>(p => p
            .Add(x => x.Suggestion, Suggestion())
            .Add(x => x.Today, new DateOnly(2026, 10, 30))
            .Add(x => x.OnDecide, (bool value) => chosen = value));

        // Act
        cut.FindAll("button").Single(x => x.TextContent.Trim() == button).Click();

        // Assert
        await Assert.That(chosen).IsEqualTo(isBill);
    }

    internal static BillSuggestion Suggestion(string name = "Streamco") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        AccountId = Guid.NewGuid(),
        AccountName = "Card",
        Frequency = BillFrequency.Monthly,
        Amount = 9.99m,
        NextDue = new DateOnly(2026, 11, 12),
        Payments =
        [
            new SuggestedPayment
            {
                TransactionId = Guid.NewGuid(), Description = "STREAMCO 111", Amount = -9.99m,
                OccurredAt = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc)
            },
            new SuggestedPayment
            {
                TransactionId = Guid.NewGuid(), Description = "STREAMCO 222", Amount = -9.99m,
                OccurredAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc)
            }
        ]
    };
}
