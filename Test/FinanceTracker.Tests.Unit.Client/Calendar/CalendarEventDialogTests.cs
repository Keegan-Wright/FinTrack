using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client.Components.Calendar;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Unit.Client.Calendar;

public class CalendarEventDialogTests : ComponentTestBase
{
    private static readonly DateOnly Day = new(2026, 3, 14);
    private static readonly Guid Gifts = Guid.NewGuid();

    private static readonly List<SavingsGoal> Goals = [new() { Id = Gifts, Name = "Gifts" }];

    private IRenderedComponent<MudDialogProvider> _dialogs = null!;
    private IRenderedComponent<MudPopoverProvider> _popovers = null!;
    private IDialogReference _dialog = null!;

    public CalendarEventDialogTests() =>
        Services.AddScoped<IValidator<SaveCalendarEvent>, SaveCalendarEventValidator>();

    [Test]
    public async Task Save_NewOneOff_IsOnTheChosenDayWithoutAGoal()
    {
        // Arrange
        await OpenAsync(null);
        Field("Title").Input("Summer trip");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        SaveCalendarEvent saved = (SaveCalendarEvent)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(saved.Title).IsEqualTo("Summer trip");
            await Assert.That(saved.Date).IsEqualTo(Day);
            await Assert.That(saved.Recurrence).IsEqualTo(EventRecurrence.None);
            await Assert.That(saved.SavingsGoalId).IsNull();
            await Assert.That(saved.CreateSavingsGoal).IsFalse();
        }
    }

    [Test]
    public async Task Save_YearlyCreatingAGoalThatMovesOn_AsksForAllOfIt()
    {
        // Arrange
        await OpenAsync(null);
        Field("Title").Input("Birthday");
        Toggle("Every year");
        Field("Save for it").Input("100");
        await ChooseAsync("Savings goal", "Create a new goal");
        Toggle("Move the goal on to next year once it's passed");

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        SaveCalendarEvent saved = (SaveCalendarEvent)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(saved.Recurrence).IsEqualTo(EventRecurrence.Yearly);
            await Assert.That(saved.SavingsTarget).IsEqualTo(100m);
            await Assert.That(saved.CreateSavingsGoal).IsTrue();
            await Assert.That(saved.SavingsGoalId).IsNull();
            await Assert.That(saved.RollsGoalOn).IsTrue();
        }
    }

    [Test]
    public async Task Save_CreatingAGoalWithNothingToSave_StaysOpenAndSaysSo()
    {
        // Arrange
        await OpenAsync(null);
        Field("Title").Input("Birthday");
        await ChooseAsync("Savings goal", "Create a new goal");

        // Act
        Button("OK").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(_dialogs.Markup).Contains("Say what to save to create a goal for it");
            await Assert.That(_dialog.Result.IsCompleted).IsFalse();
        }
    }

    [Test]
    public async Task Render_OneOff_DoesntOfferMovingTheGoalOn()
    {
        // Arrange
        await OpenAsync(null);

        // Act
        await ChooseAsync("Savings goal", "Gifts");

        // Assert
        await Assert.That(_dialogs.Markup).DoesNotContain("Move the goal on");
    }

    [Test]
    public async Task Edit_StartsFromTheEventAndItsGoal()
    {
        // Arrange
        await OpenAsync(new CalendarEvent
        {
            Id = Guid.NewGuid(), Title = "Birthday", Date = Day, Recurrence = EventRecurrence.Yearly,
            SavingsTarget = 100m, SavingsGoalId = Gifts, SavingsGoalName = "Gifts", RollsGoalOn = true
        });

        // Act
        Button("OK").Click();
        DialogResult? result = await _dialog.Result.WaitAsync(TestTimeouts.Wait);

        // Assert
        SaveCalendarEvent saved = (SaveCalendarEvent)result!.Data!;
        using (Assert.Multiple())
        {
            await Assert.That(saved.SavingsGoalId).IsEqualTo(Gifts);
            await Assert.That(saved.CreateSavingsGoal).IsFalse();
            await Assert.That(saved.RollsGoalOn).IsTrue();
        }
    }

    private async Task OpenAsync(CalendarEvent? calendarEvent)
    {
        _popovers = Render<MudPopoverProvider>();
        _dialogs = Render<MudDialogProvider>();
        IDialogService dialogService = Services.GetRequiredService<IDialogService>();
        DialogParameters<CalendarEventDialog> parameters = new()
        {
            { x => x.Event, calendarEvent },
            { x => x.Day, Day },
            { x => x.Goals, Goals }
        };

        _dialog = await _dialogs.InvokeAsync(() => dialogService.ShowAsync<CalendarEventDialog>(null, parameters));
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

    private void Toggle(string label) =>
        _dialogs.FindAll(".mud-switch")
            .Single(x => x.TextContent.Trim() == label)
            .QuerySelector("input")!
            .Change(true);

    private IElement Field(string label) =>
        _dialogs.FindAll(".mud-input-control")
            .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label)
            .QuerySelector("input")!;

    private IElement Button(string text) =>
        _dialogs.FindAll("button").Single(x => x.TextContent.Trim() == text);
}
