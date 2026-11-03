using Bunit;
using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Transactions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class SplitTransactionDialogTests : ComponentTestBase
{
    private IRenderedComponent<MudDialogProvider> _provider = null!;

    [Test]
    public async Task Open_StartsWithTwoEmptyPartsAndTheWholeAmountLeft()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        await OpenAsync(TransactionListTests.Transaction("Supermarket", -100m));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(_provider.FindAll(".ft-split__part")).Count().IsEqualTo(2);
            await Assert.That(Remaining()).IsEqualTo("£100.00 left to split");
            await Assert.That(Button("Save").HasAttribute("disabled")).IsTrue();
            await Assert.That(Button("Remove part 1").HasAttribute("disabled")).IsTrue();
        }
    }

    [Test]
    public async Task PutTheRest_FillsThePartSoItAddsUp_AndSaveReturnsTheParts()
    {
        // Arrange
        using CultureScope culture = new();
        IDialogReference dialog = await OpenAsync(TransactionListTests.Transaction("Supermarket", -100m));
        AmountInputs()[0].Input("60");

        // Act
        Button("Put the rest in part 2").Click();
        string remaining = Remaining();
        Button("Save").Click();
        DialogResult? result = await dialog.Result;

        // Assert
        SplitTransaction request = (SplitTransaction)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(remaining).IsEqualTo("The parts add up");
            await Assert.That(request.Parts.Select(static x => x.Amount)).IsEquivalentTo([-60m, -40m]);
        }
    }

    [Test]
    public async Task TooMuch_SaysBySoAndCantSave()
    {
        // Arrange
        using CultureScope culture = new();
        await OpenAsync(TransactionListTests.Transaction("Supermarket", -100m));

        // Act
        AmountInputs()[0].Input("70");
        AmountInputs()[1].Input("40");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Remaining()).IsEqualTo("£10.00 more than the transaction");
            await Assert.That(Button("Save").HasAttribute("disabled")).IsTrue();
        }
    }

    [Test]
    public async Task AddAPart_AddsOne_AndPartsCanThenBeRemoved()
    {
        // Arrange
        await OpenAsync(TransactionListTests.Transaction("Supermarket", -100m));

        // Act
        Button("Add a part").Click();
        int added = _provider.FindAll(".ft-split__part").Count;
        Button("Remove part 3").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added).IsEqualTo(3);
            await Assert.That(_provider.FindAll(".ft-split__part")).Count().IsEqualTo(2);
        }
    }

    [Test]
    public async Task Open_AlreadySplit_StartsFromItsPartsAndSavesTheirTags()
    {
        // Arrange
        Guid groceries = Guid.NewGuid();
        Transaction transaction = TransactionListTests.Transaction("Supermarket", -100m, splits:
        [
            new TransactionSplitPart { Amount = -60m, TagId = groceries, Tag = "Groceries" },
            new TransactionSplitPart { Amount = -40m }
        ]);
        IDialogReference dialog = await OpenAsync(transaction,
            [new Classification { ClassificationId = groceries, Tag = "Groceries" }]);

        // Act
        Button("Save").Click();
        DialogResult? result = await dialog.Result;

        // Assert
        SplitTransaction request = (SplitTransaction)result!.Data!;
        await Assert.That(request.Parts.Select(static x => (x.Amount, x.TagId)))
            .IsEquivalentTo([(-60m, (Guid?)groceries), (-40m, null)]);
    }

    private async Task<IDialogReference> OpenAsync(Transaction transaction, List<Classification>? tags = null)
    {
        _provider = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<SplitTransactionDialog> parameters = new()
        {
            { x => x.Transaction, transaction },
            { x => x.Tags, tags ?? [] }
        };

        return await _provider.InvokeAsync(() => dialogService.ShowAsync<SplitTransactionDialog>(null, parameters));
    }

    private IReadOnlyList<AngleSharp.Dom.IElement> AmountInputs() =>
        _provider.FindAll(".ft-split__amount input");

    private string Remaining() => _provider.Find(".ft-split__remaining").TextContent.Trim();

    private AngleSharp.Dom.IElement Button(string name) =>
        _provider.FindAll("button").Single(x =>
            x.TextContent.Trim() == name || x.GetAttribute("aria-label") == name);
}
