using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class SaveCalendarEventValidatorTests : UnitTestBase
{
    private readonly SaveCalendarEventValidator _subject = new();

    [Test]
    public async Task ValidateAsync_YearlyRollingOnACreatedGoal_IsValid()
    {
        // Arrange
        SaveCalendarEvent calendarEvent = Valid();
        calendarEvent.SavingsTarget = 100m;
        calendarEvent.CreateSavingsGoal = true;
        calendarEvent.RollsGoalOn = true;

        // Act
        ValidationResult result = await _subject.ValidateAsync(calendarEvent, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_Empty_NamesTheTitleAndDate()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveCalendarEvent(), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).IsEquivalentTo([
            nameof(SaveCalendarEvent.Title), nameof(SaveCalendarEvent.Date)
        ]);
    }

    [Test]
    public async Task ValidateAsync_TargetNotMoreThanZero_IsInvalid()
    {
        // Arrange
        SaveCalendarEvent calendarEvent = Valid();
        calendarEvent.SavingsTarget = 0m;

        // Act
        ValidationResult result = await _subject.ValidateAsync(calendarEvent, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .IsEquivalentTo(["What to save must be more than 0"]);
    }

    [Test]
    public async Task ValidateAsync_CreatingAGoalWithoutATarget_IsInvalid()
    {
        // Arrange
        SaveCalendarEvent calendarEvent = Valid();
        calendarEvent.CreateSavingsGoal = true;

        // Act
        ValidationResult result = await _subject.ValidateAsync(calendarEvent, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .IsEquivalentTo(["Say what to save to create a goal for it"]);
    }

    [Test]
    public async Task ValidateAsync_ChoosingAndCreatingAGoal_IsInvalid()
    {
        // Arrange
        SaveCalendarEvent calendarEvent = Valid();
        calendarEvent.SavingsTarget = 100m;
        calendarEvent.CreateSavingsGoal = true;
        calendarEvent.SavingsGoalId = Guid.NewGuid();

        // Act
        ValidationResult result = await _subject.ValidateAsync(calendarEvent, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .IsEquivalentTo(["Choose a goal or create one, not both"]);
    }

    [Test]
    public async Task ValidateAsync_RollingOnAOneOff_IsInvalid()
    {
        // Arrange
        SaveCalendarEvent calendarEvent = Valid();
        calendarEvent.Recurrence = EventRecurrence.None;
        calendarEvent.SavingsGoalId = Guid.NewGuid();
        calendarEvent.RollsGoalOn = true;

        // Act
        ValidationResult result = await _subject.ValidateAsync(calendarEvent, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .IsEquivalentTo(["Only a yearly event can move its goal on"]);
    }

    [Test]
    public async Task ValidateAsync_RollingOnWithoutAGoal_IsInvalid()
    {
        // Arrange
        SaveCalendarEvent calendarEvent = Valid();
        calendarEvent.RollsGoalOn = true;

        // Act
        ValidationResult result = await _subject.ValidateAsync(calendarEvent, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .IsEquivalentTo(["Choose or create a goal to move on"]);
    }

    private static SaveCalendarEvent Valid() => new()
    {
        Title = "Birthday",
        Date = new DateOnly(2026, 3, 14),
        Recurrence = EventRecurrence.Yearly
    };
}
