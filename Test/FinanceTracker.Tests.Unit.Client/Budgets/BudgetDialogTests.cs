using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Budgets;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Budgets;

public class BudgetDialogTests : ComponentTestBase
{
    private static readonly Guid Shopping = Guid.NewGuid();
    private static readonly Dictionary<Guid, string> TagNames = new() { [Shopping] = "Shopping" };

    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private IRenderedComponent<MudPopoverProvider> _popovers = null!;
    private IDialogReference _dialog = null!;

    public BudgetDialogTests() => Services.AddScoped<IValidator<SaveBudget>, SaveBudgetValidator>();

    [Test]
    public async Task Save_FilledIn_ReturnsTheBudget()
    {
        // Arrange
        await OpenAsync(null);
        Field("Name").Input("Groceries");
        await ChooseAsync("Tags it covers", "Shopping");
        Field("Monthly limit").Input("400");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        SaveBudget saved = (SaveBudget)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(saved.Name).IsEqualTo("Groceries");
            await Assert.That(saved.TagIds).IsEquivalentTo([Shopping]);
            await Assert.That(saved.MonthlyLimit).IsEqualTo(400m);
            await Assert.That(saved.RolloverMode).IsEqualTo(RolloverMode.None);
        }
    }

    [Test]
    public async Task Save_NoTag_StaysOpenAndSaysSo()
    {
        // Arrange
        await OpenAsync(null);
        Field("Name").Input("Groceries");

        // Act
        Button("OK").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(_dialogs.Markup).Contains("Choose at least one tag");
            await Assert.That(_dialog.Result.IsCompleted).IsFalse();
        }
    }

    [Test]
    public async Task Edit_StartsFromTheBudgetAndSaysWhenTheLimitApplies()
    {
        // Arrange
        Budget budget = new()
        {
            Id = Guid.NewGuid(),
            Name = "Groceries",
            TagIds = [Shopping],
            MonthlyLimit = 400m,
            RolloverMode = RolloverMode.CarryUnderspend
        };

        // Act
        await OpenAsync(budget);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Field("Name").GetAttribute("value")).IsEqualTo("Groceries");
            await Assert.That(_dialogs.Markup).Contains("A new limit applies from this month on");
            await Assert.That(Field("At the end of the month").GetAttribute("value"))
                .IsEqualTo("Carry over what's left");
        }
    }

    [Test]
    public async Task Edit_WithATagSinceDeleted_LeavesItOutSoItSaves()
    {
        // Arrange
        await OpenAsync(new Budget
        {
            Id = Guid.NewGuid(),
            Name = "Groceries",
            TagIds = [Shopping, Guid.NewGuid()],
            MonthlyLimit = 400m,
            RolloverMode = RolloverMode.None
        });

        // Act, a dialog that didn't close would leave the result waiting
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(((SaveBudget)result!.Data!).TagIds).IsEquivalentTo([Shopping]);
    }

    private async Task OpenAsync(Budget? budget)
    {
        _popovers = Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<BudgetDialog> parameters = new()
        {
            { x => x.Budget, budget },
            { x => x.TagNames, TagNames }
        };

        _dialog = await _dialogs.InvokeAsync(() => dialogService.ShowAsync<BudgetDialog>(null, parameters));
    }

    /// <summary>
    /// Opens the select labelled <paramref name="label"/> and clicks <paramref name="item"/>, as a user would.
    /// </summary>
    private async Task ChooseAsync(string label, string item)
    {
        IElement select = _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label);
        await select.MouseDownAsync(new MouseEventArgs());

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
