using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.PayPeriods;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.PayPeriods;

public class ChooseIncomeDialogTests : ComponentTestBase
{
    private readonly Income _pay = YourPays.Pay("ACME LTD", 2400m);
    private readonly Income _gig = YourPays.Pay("SIDE GIG", 300m);
    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private IRenderedComponent<MudPopoverProvider> _popovers = null!;
    private IDialogReference _dialog = null!;

    [Test]
    public async Task Open_StartsOnTheCurrentPay()
    {
        // Arrange
        await OpenAsync(current: _gig);

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        await Assert.That(((ChooseIncome)result!.Data!).Key).IsEqualTo(_gig.Key);
    }

    [Test]
    public async Task Choose_AnotherIncome_ReturnsIt()
    {
        // Arrange
        using CultureScope culture = new();
        await OpenAsync(current: _pay);
        await ChooseAsync("SIDE GIG");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        ChooseIncome chosen = (ChooseIncome)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(chosen.AccountId).IsEqualTo(_gig.AccountId);
            await Assert.That(chosen.Key).IsEqualTo(_gig.Key);
        }
    }

    private async Task OpenAsync(Income current)
    {
        _popovers = Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<ChooseIncomeDialog> parameters = new()
        {
            { x => x.Incomes, new List<Income> { _pay, _gig } },
            { x => x.Current, current }
        };

        _dialog = await _dialogs.InvokeAsync(() => dialogService.ShowAsync<ChooseIncomeDialog>(null, parameters));
    }

    /// <summary>
    /// Opens the select and clicks the income from <paramref name="payer"/>, as a user would.
    /// </summary>
    private async Task ChooseAsync(string payer)
    {
        IElement select = _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == "Your pay");
        await select.MouseDownAsync(new MouseEventArgs());

        IElement option = _popovers.WaitForElements(".mud-popover-open .mud-list-item")
            .Single(x => x.TextContent.Trim().StartsWith($"{payer} · ", StringComparison.Ordinal));
        await option.ClickAsync(new MouseEventArgs());
    }

    private IElement Button(string text) =>
        _dialogs.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
