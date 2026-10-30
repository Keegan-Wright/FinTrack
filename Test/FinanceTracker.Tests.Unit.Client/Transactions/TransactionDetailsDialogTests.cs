using Bunit;
using FinanceTracker.Client.Components.Dialogs;
using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Transactions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

    [Test]
    public async Task Render_TagFromARule_SaysWhichRule()
    {
        // Arrange
        Transaction transaction = TransactionListTests.Transaction("Coffee", -3.5m);
        transaction.Tags = ["Treat"];
        transaction.TagsFromRules.Add(
            new TransactionRuleTag { Tag = "Treat", RuleId = Guid.NewGuid(), RuleName = "Cafes" });

        // Act
        await OpenAsync(transaction);

        // Assert
        await Assert.That(_provider.Find(".ft-details").TextContent).Contains("Treat from your rule Cafes");
    }

    [Test]
    public async Task Choose_CreateRule_ClosesAndRaisesIt()
    {
        // Arrange
        bool createRule = false;
        await OpenAsync(TransactionListTests.Transaction("Coffee", -3.5m), onCreateRule: () => createRule = true);

        // Act
        Button("Create rule from this").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(createRule).IsTrue();
            await Assert.That(_provider.FindAll(".ft-details")).IsEmpty();
        }
    }

    [Test]
    public async Task HideBankClassification_StaysOpenShowingItHidden()
    {
        // Arrange
        Transaction mortgage = WithBankClassifications(TransactionListTests.Transaction("Mortgage", -950m));
        Transaction afterHiding = WithBankClassifications(mortgage, hidden: true);
        string? asked = null;
        await OpenAsync(mortgage, onHide: classification =>
        {
            asked = classification;
            return Task.FromResult<Transaction?>(afterHiding);
        });

        // Act
        await _provider.FindAll(".mud-chip")
            .Single(x => x.TextContent.Trim() == "TV")
            .QuerySelector("button")!
            .ClickAsync(new MouseEventArgs());

        // Assert
        string details = _provider.Find(".ft-details").TextContent;
        using (Assert.Multiple())
        {
            await Assert.That(asked).IsEqualTo("TV");
            await Assert.That(details).Contains("TV hidden by you");
            await Assert.That(_provider.FindAll("button").Any(x => x.TextContent.Trim() == "Show")).IsTrue();
        }
    }

    [Test]
    public async Task Render_HiddenByARule_SaysWhichAndCantBeShownHere()
    {
        // Arrange
        Transaction mortgage = WithBankClassifications(TransactionListTests.Transaction("Mortgage", -950m),
            hidden: true, ruleName: "Mortgage");

        // Act
        await OpenAsync(mortgage);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(_provider.Find(".ft-details").TextContent).Contains("TV hidden by your rule Mortgage");
            await Assert.That(_provider.FindAll("button").Any(x => x.TextContent.Trim() == "Show")).IsFalse();
        }
    }

    /// <summary>
    /// <paramref name="transaction"/> as the bank classified it, Bills and TV, with TV hidden when asked.
    /// </summary>
    private static Transaction WithBankClassifications(Transaction transaction, bool hidden = false,
        string? ruleName = null) => new()
    {
        TransactionId = transaction.TransactionId,
        Description = transaction.Description,
        TransactionType = transaction.TransactionType,
        TransactionCategory = transaction.TransactionCategory,
        Amount = transaction.Amount,
        Currency = transaction.Currency,
        TransactionTime = transaction.TransactionTime,
        Pending = transaction.Pending,
        AccountName = transaction.AccountName,
        Tags = hidden ? ["Bills"] : ["Bills", "TV"],
        BankClassifications = hidden ? ["Bills"] : ["Bills", "TV"],
        HiddenBankClassifications = hidden
            ? [new TransactionHiddenClassification { Classification = "TV", RuleName = ruleName }]
            : []
    };

    private async Task OpenAsync(Transaction transaction, Action<TransferAction>? onTransferAction = null,
        Action? onAddTags = null, Action? onCreateRule = null, Func<string, Task<Transaction?>>? onHide = null)
    {
        _provider = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<TransactionDetailsDialog>
        {
            { x => x.Transaction, transaction },
            { x => x.OnTransferAction, EventCallback.Factory.Create(this, onTransferAction ?? (_ => { })) },
            { x => x.OnAddTags, EventCallback.Factory.Create(this, onAddTags ?? (() => { })) },
            { x => x.OnCreateRule, EventCallback.Factory.Create(this, onCreateRule ?? (() => { })) },
            { x => x.OnHideClassification, onHide }
        };

        await _provider.InvokeAsync(() => dialogService.ShowAsync<TransactionDetailsDialog>(null, parameters));
    }

    private AngleSharp.Dom.IElement Button(string text) =>
        _provider.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
