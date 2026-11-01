using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Bills;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tests.Unit.Client.Transactions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Bills;

public class PayBillDialogTests : ComponentTestBase
{
    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private IRenderedComponent<MudPopoverProvider> _popovers = null!;
    private IDialogReference _dialog = null!;

    [Test]
    public async Task Choose_ABill_ReturnsIt()
    {
        // Arrange
        Bill energy = BillTextTests.Bill();
        await OpenAsync(TransactionListTests.Transaction("ENERGY CO", -42m), [energy]);
        await ChooseAsync("Energy");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(((PayBill)result!.Data!).BillId).IsEqualTo(energy.Id);
    }

    [Test]
    public async Task Choose_NoBill_ReturnsNone()
    {
        // Arrange
        Bill energy = BillTextTests.Bill();
        Transaction transaction = TransactionListTests.Transaction("ENERGY CO", -42m,
            bill: new TransactionBill { BillId = energy.Id, BillName = energy.Name });
        await OpenAsync(transaction, [energy]);
        await ChooseAsync("No bill");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(((PayBill)result!.Data!).BillId).IsNull();
    }

    private async Task OpenAsync(Transaction transaction, List<Bill> bills)
    {
        _popovers = Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<PayBillDialog> parameters = new()
        {
            { x => x.Transaction, transaction },
            { x => x.Bills, bills }
        };

        _dialog = await _dialogs.InvokeAsync(() => dialogService.ShowAsync<PayBillDialog>(null, parameters));
    }

    /// <summary>
    /// Opens the bill select and clicks <paramref name="item"/>, as a user would.
    /// </summary>
    private async Task ChooseAsync(string item)
    {
        IElement select = _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == "Pays");
        await select.MouseDownAsync(new MouseEventArgs());

        IElement option = _popovers.WaitForElements(".mud-popover-open .mud-list-item")
            .Single(x => x.TextContent.Trim() == item);
        await option.ClickAsync(new MouseEventArgs());
    }

    private IElement Button(string text) =>
        _dialogs.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
