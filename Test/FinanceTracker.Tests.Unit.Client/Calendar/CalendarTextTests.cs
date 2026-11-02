using FinanceTracker.Client.Components.Calendar;
using FinanceTracker.Contracts.Calendar;

namespace FinanceTracker.Tests.Unit.Client.Calendar;

public class CalendarTextTests
{
    [Test]
    public async Task Saving_InAGoal_SaysWhatsSavedOfTheTarget()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string? saving = CalendarText.Saving(Event(target: 100m, goalName: "Gifts", saved: 40m));
        string? noTarget = CalendarText.Saving(Event(goalName: "Gifts", saved: 40m));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(saving).IsEqualTo("£40.00 of £100.00 saved in Gifts");
            await Assert.That(noTarget).IsEqualTo("£40.00 saved in Gifts");
        }
    }

    [Test]
    public async Task Saving_WithoutAGoal_SaysWhatToSaveOrNothing()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string? toSave = CalendarText.Saving(Event(target: 100m));
        string? nothing = CalendarText.Saving(Event());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(toSave).IsEqualTo("Save £100.00 for it");
            await Assert.That(nothing).IsNull();
        }
    }

    [Test]
    [Arguments(CalendarDebtDate.PaidOff, null, "Forecast to be paid off")]
    [Arguments(CalendarDebtDate.Goal, true, "Your goal to pay it off, on track")]
    [Arguments(CalendarDebtDate.Goal, false, "Your goal to pay it off, behind")]
    public async Task DebtDate_SaysWhichDateItIs(CalendarDebtDate kind, bool? onTrack, string expected)
    {
        // Act
        string text = CalendarText.DebtDate(new CalendarDebtItem { Name = "Car loan", Kind = kind, OnTrack = onTrack });

        // Assert
        await Assert.That(text).IsEqualTo(expected);
    }

    internal static CalendarEventItem Event(string title = "Birthday", decimal? target = null,
        string? goalName = null, decimal? saved = null, bool yearly = true, string? notes = null) => new()
    {
        EventId = Guid.NewGuid(),
        Title = title,
        Notes = notes,
        IsYearly = yearly,
        SavingsTarget = target,
        SavingsGoalName = goalName,
        Saved = saved
    };
}
