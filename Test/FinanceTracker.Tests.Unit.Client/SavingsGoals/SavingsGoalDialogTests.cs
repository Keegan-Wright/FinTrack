using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.SavingsGoals;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.SavingsGoals;

public class SavingsGoalDialogTests : ComponentTestBase
{
    private static readonly Guid RainyDay = Guid.NewGuid();
    private static readonly Guid Holiday = Guid.NewGuid();

    private static readonly List<SavingsGoalAccount> Accounts =
    [
        new() { AccountId = RainyDay, Name = "Rainy Day" },
        new() { AccountId = Holiday, Name = "Holiday pot", GoalId = Guid.NewGuid() }
    ];

    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private IRenderedComponent<MudPopoverProvider> _popovers = null!;
    private IDialogReference _dialog = null!;

    public SavingsGoalDialogTests() =>
        Services.AddScoped<IValidator<SaveSavingsGoal>, SaveSavingsGoalValidator>();

    [Test]
    public async Task Save_BackedByAnAccount_ReturnsTheAccount()
    {
        // Arrange
        await OpenAsync(null);
        Field("Name").Input("Car");
        await ChooseAsync("What's saved comes from", "A savings account's balance");
        await ChooseAsync("Account", "Rainy Day");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        SaveSavingsGoal saved = (SaveSavingsGoal)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(saved.Source).IsEqualTo(SavingsGoalSource.Account);
            await Assert.That(saved.AccountId).IsEqualTo(RainyDay);
            await Assert.That(saved.TagIds).IsEmpty();
        }
    }

    [Test]
    public async Task Accounts_BackingAnotherGoal_AreNotOffered()
    {
        // Arrange
        await OpenAsync(null);
        await ChooseAsync("What's saved comes from", "A savings account's balance");

        // Act
        await OpenSelectAsync("Account");

        // Assert
        IReadOnlyList<string> offered = _popovers.WaitForElements(".mud-popover-open .mud-list-item")
            .Select(static x => x.TextContent.Trim()).ToList();
        await Assert.That(offered).IsEquivalentTo(["Rainy Day"]);
    }

    [Test]
    public async Task Save_TagsWithoutATag_StaysOpenAndSaysSo()
    {
        // Arrange
        await OpenAsync(null);
        Field("Name").Input("Car");
        await ChooseAsync("What's saved comes from", "Tagged transactions");

        // Act
        Button("OK").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(_dialogs.Markup).Contains("Choose at least one tag");
            await Assert.That(_dialog.Result.IsCompleted).IsFalse();
        }
    }

    private async Task OpenAsync(SavingsGoal? goal)
    {
        _popovers = Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<SavingsGoalDialog> parameters = new()
        {
            { x => x.Goal, goal },
            { x => x.Accounts, Accounts },
            { x => x.TagNames, new Dictionary<Guid, string>() }
        };

        _dialog = await _dialogs.InvokeAsync(() => dialogService.ShowAsync<SavingsGoalDialog>(null, parameters));
    }

    /// <summary>
    /// Opens the select labelled <paramref name="label"/> and clicks <paramref name="item"/>, as a user would.
    /// </summary>
    private async Task ChooseAsync(string label, string item)
    {
        await OpenSelectAsync(label);

        IElement option = _popovers.WaitForElements(".mud-popover-open .mud-list-item")
            .Single(x => x.TextContent.Trim() == item);
        await option.ClickAsync(new MouseEventArgs());
    }

    /// <summary>
    /// MudSelect opens as the button goes down, like a native select.
    /// </summary>
    private Task OpenSelectAsync(string label) =>
        _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label)
            .MouseDownAsync(new MouseEventArgs());

    private IElement Field(string label) =>
        _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label)
            .QuerySelector("input")!;

    private IElement Button(string text) =>
        _dialogs.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
