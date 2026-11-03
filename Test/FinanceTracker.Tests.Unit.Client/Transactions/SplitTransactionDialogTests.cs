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
        _provider.WaitForState(() => Remaining() == "£40.00 left to split");

        // Act
        Button("Put the rest in part 2").Click();
        _provider.WaitForState(() => Remaining() == "The parts add up");
        Button("Save").Click();
        DialogResult? result = await dialog.Result.WaitAsync(Timeout);

        // Assert
        SplitTransaction request = (SplitTransaction)result!.Data!;
        await Assert.That(request.Parts.Select(static x => x.Amount)).IsEquivalentTo([-60m, -40m]);
    }

    [Test]
    public async Task TooMuch_SaysBySoAndCantSave()
    {
        // Arrange
        using CultureScope culture = new();
        await OpenAsync(TransactionListTests.Transaction("Supermarket", -100m));

        // Act
        AmountInputs()[0].Input("70");
        _provider.WaitForState(() => Remaining() == "£30.00 left to split");
        AmountInputs()[1].Input("40");

        // Assert
        _provider.WaitForState(() => Remaining() == "£10.00 more than the transaction");
        await Assert.That(Button("Save").HasAttribute("disabled")).IsTrue();
    }

    [Test]
    public async Task AddAPart_AddsOne_AndPartsCanThenBeRemoved()
    {
        // Arrange
        await OpenAsync(TransactionListTests.Transaction("Supermarket", -100m));

        // Act
        Button("Add a part").Click();
        _provider.WaitForState(() => _provider.FindAll(".ft-split__part").Count == 3);
        Button("Remove part 3").Click();

        // Assert
        _provider.WaitForState(() => _provider.FindAll(".ft-split__part").Count == 2);
        await Assert.That(_provider.FindAll(".ft-split__part")).Count().IsEqualTo(2);
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
        DialogResult? result = await dialog.Result.WaitAsync(Timeout);

        // Assert
        SplitTransaction request = (SplitTransaction)result!.Data!;
        await Assert.That(request.Parts.Select(static x => (x.Amount, x.TagId)))
            .IsEquivalentTo([(-60m, (Guid?)groceries), (-40m, null)]);
    }

    /// <summary>
    /// How long the dialog gets to close, so a click that didn't land fails rather than waits forever.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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
