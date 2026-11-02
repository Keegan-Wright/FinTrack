using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Debts;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tests.Unit.Client.Transactions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Debts;

public class PayDebtDialogTests : ComponentTestBase
{
    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private IRenderedComponent<MudPopoverProvider> _popovers = null!;
    private IDialogReference _dialog = null!;

    [Test]
    public async Task Choose_ADebt_ReturnsIt()
    {
        // Arrange
        Debt carLoan = DebtTextTests.Debt();
        await OpenAsync(TransactionListTests.Transaction("CAR FINANCE", -250m), [carLoan]);
        await ChooseAsync("Car loan");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(((PayDebt)result!.Data!).DebtId).IsEqualTo(carLoan.Id);
    }

    [Test]
    public async Task Choose_NoDebt_ReturnsNone()
    {
        // Arrange
        Debt carLoan = DebtTextTests.Debt();
        Transaction transaction = TransactionListTests.Transaction("CAR FINANCE", -250m,
            debt: new TransactionDebt { DebtId = carLoan.Id, DebtName = carLoan.Name });
        await OpenAsync(transaction, [carLoan]);
        await ChooseAsync("No debt");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(((PayDebt)result!.Data!).DebtId).IsNull();
    }

    [Test]
    public async Task Render_NoDebts_PointsToTheDebtsPage()
    {
        // Act
        await OpenAsync(TransactionListTests.Transaction("CAR FINANCE", -250m), []);

        // Assert
        await Assert.That(_dialogs.FindAll("a[href='Debts']")).Count().IsEqualTo(1);
    }

    private async Task OpenAsync(Transaction transaction, List<Debt> debts)
    {
        _popovers = Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<PayDebtDialog> parameters = new()
        {
            { x => x.Transaction, transaction },
            { x => x.Debts, debts }
        };

        _dialog = await _dialogs.InvokeAsync(() => dialogService.ShowAsync<PayDebtDialog>(null, parameters));
    }

    /// <summary>
    /// Opens the debt select and clicks <paramref name="item"/>, as a user would.
    /// </summary>
    private async Task ChooseAsync(string item)
    {
        IElement select = _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == "Pays off");
        await select.MouseDownAsync(new MouseEventArgs());

        IElement option = _popovers.WaitForElements(".mud-popover-open .mud-list-item")
            .Single(x => x.TextContent.Trim() == item);
        await option.ClickAsync(new MouseEventArgs());
    }

    private IElement Button(string text) =>
        _dialogs.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
