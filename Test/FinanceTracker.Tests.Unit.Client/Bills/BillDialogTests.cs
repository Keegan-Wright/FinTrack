using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Bills;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Client.Bills;

public class BillDialogTests : ComponentTestBase
{
    private static readonly TransactionAccountFilter Current = new() { AccountId = Guid.NewGuid(), AccountName = "Current" };

    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private IDialogReference _dialog = null!;

    public BillDialogTests() => Services.AddScoped<IValidator<SaveBill>, SaveBillValidator>();

    [Test]
    public async Task Save_FilledIn_ReturnsTheBill()
    {
        // Arrange
        using CultureScope culture = new();
        await OpenAsync(null);
        Type("Name", "Gym");
        Type("Paid to", "GYMCO");
        Choose("First due", "12/11/2026");
        Type("Amount", "30");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        SaveBill saved = (SaveBill)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(saved.Name).IsEqualTo("Gym");
            await Assert.That(saved.Payee).IsEqualTo("GYMCO");
            await Assert.That(saved.AccountId).IsEqualTo(Current.AccountId);
            await Assert.That(saved.Frequency).IsEqualTo(BillFrequency.Monthly);
            await Assert.That(saved.ExpectedAmount).IsEqualTo(30m);
            await Assert.That(saved.TolerancePercent).IsEqualTo(5m);
        }
    }

    [Test]
    public async Task Save_NoDueDate_StaysOpenAndSaysSo()
    {
        // Arrange
        using CultureScope culture = new();
        await OpenAsync(null);
        Type("Name", "Gym");
        Type("Paid to", "GYMCO");
        Type("Amount", "30");

        // Act
        Button("OK").Click();

        // Assert
        _dialogs.WaitForState(() => _dialogs.Markup.Contains("Choose when it's next due"));
        await Assert.That(_dialog.Result.IsCompleted).IsFalse();
    }

    [Test]
    public async Task Edit_StandingOrder_LeavesTheScheduleAndAmountToTheBank()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        await OpenAsync(BillTextTests.Bill("Rent", source: BillSource.StandingOrder));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Field("Name").GetAttribute("value")).IsEqualTo("Rent");
            await Assert.That(Field("Amount").HasAttribute("disabled")).IsTrue();
            await Assert.That(Field("Next due").HasAttribute("disabled")).IsTrue();
            await Assert.That(Field("Paid from").HasAttribute("disabled")).IsTrue();
            await Assert.That(_dialogs.Markup).Contains("your bank keeps its schedule and amount up to date");
        }
    }

    /// <remarks>
    /// The test holds a <see cref="CultureScope"/> for as long as it runs, dates are typed and read in its culture.
    /// </remarks>
    private async Task OpenAsync(Bill? bill)
    {
        Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<BillDialog> parameters = new()
        {
            { x => x.Bill, bill },
            { x => x.Accounts, new List<TransactionAccountFilter> { Current } }
        };

        _dialog = await _dialogs.InvokeAsync(() => dialogService.ShowAsync<BillDialog>(null, parameters));
    }

    /// <summary>
    /// Types into the field labelled <paramref name="label"/> and waits for it to take the text, the field applies it
    /// after the input event returns.
    /// </summary>
    private void Type(string label, string text)
    {
        Field(label).Input(text);
        _dialogs.WaitForState(() => Field(label).GetAttribute("value") == text);
    }

    /// <summary>
    /// Enters a date as the picker's field takes one, and waits for it to take it.
    /// </summary>
    private void Choose(string label, string date)
    {
        Field(label).Change(date);
        _dialogs.WaitForState(() => Field(label).GetAttribute("value") == date);
    }

    private IElement Field(string label) =>
        _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label)
            .QuerySelector("input")!;

    private IElement Button(string text) =>
        _dialogs.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
