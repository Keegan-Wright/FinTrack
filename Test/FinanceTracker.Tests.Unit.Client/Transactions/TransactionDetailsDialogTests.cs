using Bunit;
using FinanceTracker.Client.Components.Dialogs;
using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Transactions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class TransactionDetailsDialogTests : ComponentTestBase
{
    private IRenderedComponent<MudDialogProvider> _provider = null!;

    [Test]
    public async Task Render_ShowsEveryFieldAndWhyItCounts()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        await OpenAsync(TransactionListTests.Transaction("To savings", -20m, transfer: new TransactionTransfer
        {
            State = TransferState.NeedsReview,
            DecidedBy = TransferDecidedBy.FinTrack,
            Reason = TransferMatchReason.BankTransferLabel
        }));

        // Assert
        string details = _provider.Find(".ft-details").TextContent;

        using (Assert.Multiple())
        {
            await Assert.That(details).Contains("Current");
            await Assert.That(details).Contains("4 Oct 2026");
            await Assert.That(details).Contains("Purchase");
            await Assert.That(details).Contains("Review transfer");
            await Assert.That(details).Contains("the other half isn't on any of your accounts");
        }
    }

    [Test]
    public async Task Choose_TransferAction_ClosesAndRaisesIt()
    {
        // Arrange
        TransferAction? chosen = null;
        await OpenAsync(TransactionListTests.Transaction("Coffee", -3.5m),
            onTransferAction: action => chosen = action);

        // Act
        Button("It's a transfer").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(chosen).IsEqualTo(TransferAction.MarkAsTransfer);
            await Assert.That(_provider.FindAll(".ft-details")).IsEmpty();
        }
    }

    [Test]
    public async Task Choose_AddTags_ClosesAndRaisesIt()
    {
        // Arrange
        bool addTags = false;
        await OpenAsync(TransactionListTests.Transaction("Coffee", -3.5m), onAddTags: () => addTags = true);

        // Act
        Button("Add tags").Click();

        // Assert
        await Assert.That(addTags).IsTrue();
    }

    private async Task OpenAsync(Transaction transaction, Action<TransferAction>? onTransferAction = null,
        Action? onAddTags = null)
    {
        _provider = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<TransactionDetailsDialog>
        {
            { x => x.Transaction, transaction },
            { x => x.OnTransferAction, EventCallback.Factory.Create(this, onTransferAction ?? (_ => { })) },
            { x => x.OnAddTags, EventCallback.Factory.Create(this, onAddTags ?? (() => { })) }
        };

        await _provider.InvokeAsync(() => dialogService.ShowAsync<TransactionDetailsDialog>(null, parameters));
    }

    private AngleSharp.Dom.IElement Button(string text) =>
        _provider.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
