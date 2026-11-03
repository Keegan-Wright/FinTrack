using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Debts;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Client.Debts;

public class DebtDialogTests : ComponentTestBase
{
    private static readonly DateOnly Today = new(2026, 11, 2);
    private static readonly Guid LoanAccount = Guid.NewGuid();

    private static readonly List<TransactionAccountFilter> Accounts =
    [
        new() { AccountId = LoanAccount, AccountName = "Loan account" }
    ];

    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private IRenderedComponent<MudPopoverProvider> _popovers = null!;
    private IDialogReference _dialog = null!;

    public DebtDialogTests() => Services.AddScoped<IValidator<SaveDebt>, SaveDebtValidator>();

    [Test]
    public async Task Save_NewDebt_ReturnsWhatWasEnteredStartingToday()
    {
        // Arrange
        await OpenAsync(null);
        Field("Name").Input("Car loan");
        Field("Lender").Input("Car Finance Co");
        Field("Owed").Input("1000");
        Field("Interest rate").Input("6.9");
        Field("Monthly payment").Input("250");
        await ChooseAsync("Balance from", "Loan account");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        SaveDebt saved = (SaveDebt)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(saved.Name).IsEqualTo("Car loan");
            await Assert.That(saved.Lender).IsEqualTo("Car Finance Co");
            await Assert.That(saved.StartingBalance).IsEqualTo(1000m);
            await Assert.That(saved.StartedOn).IsEqualTo(Today);
            await Assert.That(saved.InterestRate).IsEqualTo(6.9m);
            await Assert.That(saved.MonthlyPayment).IsEqualTo(250m);
            await Assert.That(saved.AccountId).IsEqualTo(LoanAccount);
            await Assert.That(saved.PayOffGoal).IsNull();
        }
    }

    [Test]
    public async Task Save_NothingOwed_StaysOpenAndSaysSo()
    {
        // Arrange
        await OpenAsync(null);
        Field("Name").Input("Car loan");
        Field("Monthly payment").Input("250");

        // Act
        Button("OK").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(_dialogs.Markup).Contains("What was owed must be more than 0");
            await Assert.That(_dialog.Result.IsCompleted).IsFalse();
        }
    }

    [Test]
    public async Task Edit_StartsFromTheDebtAsItIs()
    {
        // Arrange
        Debt debt = DebtTextTests.Debt(lender: "Car Finance Co", payOffGoal: new DateOnly(2028, 6, 1));
        await OpenAsync(debt);

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        SaveDebt saved = (SaveDebt)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(saved.Name).IsEqualTo(debt.Name);
            await Assert.That(saved.StartedOn).IsEqualTo(debt.StartedOn);
            await Assert.That(saved.PayOffGoal).IsEqualTo(new DateOnly(2028, 6, 1));
            await Assert.That(saved.AccountId).IsNull();
        }
    }

    private async Task OpenAsync(Debt? debt)
    {
        _popovers = Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<DebtDialog> parameters = new()
        {
            { x => x.Debt, debt },
            { x => x.Accounts, Accounts },
            { x => x.Today, Today }
        };

        _dialog = await _dialogs.InvokeAsync(() => dialogService.ShowAsync<DebtDialog>(null, parameters));
    }

    /// <summary>
    /// Opens the select labelled <paramref name="label"/> and clicks <paramref name="item"/>, as a user would.
    /// </summary>
    private async Task ChooseAsync(string label, string item)
    {
        await _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label)
            .MouseDownAsync(new MouseEventArgs());

        IElement option = _popovers.WaitForElements(".mud-popover-open .mud-list-item")
            .Single(x => x.TextContent.Trim() == item);
        await option.ClickAsync(new MouseEventArgs());
    }

    private IElement Field(string label) =>
        _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label)
            .QuerySelector("input")!;

    private IElement Button(string text) =>
        _dialogs.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
