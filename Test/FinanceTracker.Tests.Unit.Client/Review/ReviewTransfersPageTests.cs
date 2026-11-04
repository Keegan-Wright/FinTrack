using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Pages;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Review;

public class ReviewTransfersPageTests : ComponentTestBase
{
    private readonly ITransfersService _transfersService = Substitute.For<ITransfersService>();
    private readonly ApplicationState _applicationState = new();

    public ReviewTransfersPageTests()
    {
        Services.AddSingleton(_transfersService);
        SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
    }

    [Test]
    public async Task Synced_AddsNewTransactionsAndKeepsAnsweredOnesOff()
    {
        // Arrange
        Transaction answered = ReviewPairRowTests.Transaction("Paid someone", "Current", -20m);
        Transaction waiting = ReviewPairRowTests.Transaction("To savings", "Current", -50m);
        Transaction added = ReviewPairRowTests.Transaction("From a friend", "Current", 30m);
        List<Transaction> toReview = [answered, waiting];
        _transfersService.GetTransfersToReviewAsync(Arg.Any<CancellationToken>())
            .Returns(_ => toReview.ToAsyncEnumerable());
        _transfersService.MarkNotATransferAsync(answered.TransactionId, Arg.Any<CancellationToken>())
            .Returns([
                ReviewPairRowTests.Transaction("Paid someone", "Current", -20m, TransferState.NotATransfer,
                    answered.TransactionId)
            ]);
        Render<MudBlazor.MudSnackbarProvider>();
        IRenderedComponent<ReviewTransfers> cut = RenderPage();
        cut.WaitForState(() => cut.FindAll(".ft-review").Count == 2);
        await cut.InvokeAsync(() => cut.FindAll(".ft-review")
            .Single(x => x.TextContent.Contains("Paid someone"))
            .QuerySelectorAll("button").Single(x => x.TextContent.Trim() == "Not a transfer").Click());
        cut.WaitForState(() => cut.FindAll(".ft-review").Count == 1);

        // A refresh that started before the answer was saved still has it
        toReview = [answered, waiting, added];

        // Act
        await cut.InvokeAsync(_applicationState.NotifySyncedAsync);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.FindAll(".ft-review")).Count().IsEqualTo(2);
            await Assert.That(cut.Markup).Contains("To savings");
            await Assert.That(cut.Markup).Contains("From a friend");
            await Assert.That(cut.Markup).DoesNotContain("Paid someone");
        }
    }

    private IRenderedComponent<ReviewTransfers> RenderPage() =>
        Render<ReviewTransfers>(p => p.AddCascadingValue(_applicationState));
}
