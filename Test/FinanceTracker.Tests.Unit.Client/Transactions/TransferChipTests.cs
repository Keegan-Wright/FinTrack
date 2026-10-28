using Bunit;
using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Transactions;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class TransferChipTests : ComponentTestBase
{
    [Test]
    public async Task Render_NoTransfer_RendersNothing()
    {
        // Act
        IRenderedComponent<TransferChip> cut = Render<TransferChip>();

        // Assert
        await Assert.That(cut.Markup.Trim()).IsEmpty();
    }

    [Test]
    public async Task Render_Transfer_ShowsTheLabel()
    {
        // Act
        IRenderedComponent<TransferChip> cut = Render<TransferChip>(p => p.Add(x => x.Transfer, Transfer()));

        // Assert
        await Assert.That(cut.Find(".mud-chip").TextContent.Trim()).IsEqualTo("Review transfer");
    }

    [Test]
    public async Task Open_ShowsWhyAndTheOtherHalfAndRaisesTheChosenAction()
    {
        // Arrange
        TransferAction? chosen = null;
        IRenderedComponent<MudPopoverProvider> popovers = Render<MudPopoverProvider>();
        IRenderedComponent<TransferChip> cut = Render<TransferChip>(p => p
            .Add(x => x.Transfer, Transfer())
            .Add(x => x.OnAction, (TransferAction action) => chosen = action));

        // Act
        cut.Find(".mud-chip").Click();
        popovers.WaitForState(() => popovers.FindAll(".mud-menu-item").Count > 0);
        string menu = popovers.Find(".ft-transfer").TextContent;
        popovers.FindAll(".mud-menu-item").Single(x => x.TextContent.Trim() == "It's a transfer").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(menu).Contains("the bank didn't call it a transfer");
            await Assert.That(menu).Contains("Other half: To savings");
            await Assert.That(chosen).IsEqualTo(TransferAction.MarkAsTransfer);
        }
    }

    private static TransactionTransfer Transfer() => new()
    {
        State = TransferState.NeedsReview,
        DecidedBy = TransferDecidedBy.FinTrack,
        Reason = TransferMatchReason.MirrorPair,
        PartnerDescription = "To savings",
        PartnerAccountName = "Savings"
    };
}
