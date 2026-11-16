using FinanceTracker.Domain.Calendar;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.SavingsGoals;

namespace FinanceTracker.Tests.Unit.Domain.Calendar;

[Category("Domain")]
public class CalendarEventTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Birthday = new(2026, 3, 14);

    [Test]
    public async Task Constructor_BlankTitle_Throws() =>
        await Assert.That(() => new CalendarEvent(UserId, "  ", Birthday, EventRecurrence.Yearly))
            .Throws<ArgumentException>();

    [Test]
    public async Task Constructor_TrimsTheTitleAndStartsWithNothingToSave()
    {
        // Act
        CalendarEvent calendarEvent = new(UserId, "  Birthday  ", Birthday, EventRecurrence.Yearly);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(calendarEvent.Title).IsEqualTo("Birthday");
            await Assert.That(calendarEvent.Notes).IsNull();
            await Assert.That(calendarEvent.SavingsTarget).IsNull();
            await Assert.That(calendarEvent.SavingsGoalId).IsNull();
            await Assert.That(calendarEvent.RollsGoalOn).IsFalse();
        }
    }

    [Test]
    [Arguments(2026, 3, 14, true)]
    [Arguments(2027, 3, 14, true)]
    [Arguments(2025, 3, 14, false)]
    [Arguments(2027, 3, 15, false)]
    public async Task OccursOn_Yearly_EveryYearFromItsDate(int year, int month, int day, bool expected)
    {
        // Arrange
        CalendarEvent calendarEvent = new(UserId, "Birthday", Birthday, EventRecurrence.Yearly);

        // Act
        bool occurs = calendarEvent.OccursOn(new DateOnly(year, month, day));

        // Assert
        await Assert.That(occurs).IsEqualTo(expected);
    }

    [Test]
    public async Task OccursOn_OneOff_OnlyOnItsDate()
    {
        // Arrange
        CalendarEvent trip = new(UserId, "Trip", Birthday, EventRecurrence.None);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(trip.OccursOn(Birthday)).IsTrue();
            await Assert.That(trip.OccursOn(Birthday.AddYears(1))).IsFalse();
        }
    }

    [Test]
    public async Task OccursOn_TwentyNinthOfFebruary_FallsOnTheTwentyEighthOutsideLeapYears()
    {
        // Arrange
        CalendarEvent leapling = new(UserId, "Birthday", new DateOnly(2028, 2, 29), EventRecurrence.Yearly);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(leapling.OccursOn(new DateOnly(2029, 2, 28))).IsTrue();
            await Assert.That(leapling.OccursOn(new DateOnly(2032, 2, 28))).IsFalse();
            await Assert.That(leapling.OccursOn(new DateOnly(2032, 2, 29))).IsTrue();
        }
    }

    [Test]
    [Arguments(2026, 1, 1, 2026, 3, 14)]
    [Arguments(2026, 3, 14, 2026, 3, 14)]
    [Arguments(2026, 3, 15, 2027, 3, 14)]
    [Arguments(2030, 6, 1, 2031, 3, 14)]
    public async Task NextOn_Yearly_IsTheNextTimeItsOn(int fromYear, int fromMonth, int fromDay, int year, int month,
        int day)
    {
        // Arrange
        CalendarEvent calendarEvent = new(UserId, "Birthday", Birthday, EventRecurrence.Yearly);

        // Act
        DateOnly? next = calendarEvent.NextOn(new DateOnly(fromYear, fromMonth, fromDay));

        // Assert
        await Assert.That(next).IsEqualTo(new DateOnly(year, month, day));
    }

    [Test]
    public async Task NextOn_PassedOneOff_IsNone()
    {
        // Arrange
        CalendarEvent trip = new(UserId, "Trip", Birthday, EventRecurrence.None);

        // Act
        DateOnly? next = trip.NextOn(Birthday.AddDays(1));

        // Assert
        await Assert.That(next).IsNull();
    }

    [Test]
    public async Task SetSavingsTarget_NotMoreThanZero_Throws()
    {
        // Arrange
        CalendarEvent calendarEvent = new(UserId, "Birthday", Birthday, EventRecurrence.Yearly);

        // Act & Assert
        await Assert.That(() => calendarEvent.SetSavingsTarget(0m)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task LinkSavingsGoal_AnotherUsersGoal_Throws()
    {
        // Arrange
        CalendarEvent calendarEvent = new(UserId, "Birthday", Birthday, EventRecurrence.Yearly);
        SavingsGoal theirs = Goal(Guid.NewGuid(), null);

        // Act & Assert
        await Assert.That(() => calendarEvent.LinkSavingsGoal(theirs)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SetRollsGoalOn_OneOffOrWithoutAGoal_Throws()
    {
        // Arrange
        CalendarEvent trip = new(UserId, "Trip", Birthday, EventRecurrence.None);
        trip.LinkSavingsGoal(Goal(UserId, null));
        CalendarEvent noGoal = new(UserId, "Birthday", Birthday, EventRecurrence.Yearly);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(() => trip.SetRollsGoalOn(true)).Throws<InvalidOperationException>();
            await Assert.That(() => noGoal.SetRollsGoalOn(true)).Throws<InvalidOperationException>();
        }
    }

    [Test]
    public async Task UnlinkingTheGoalOrMakingItOneOff_StopsRollingOn()
    {
        // Arrange
        CalendarEvent unlinked = Rolling(Goal(UserId, null));
        CalendarEvent oneOff = Rolling(Goal(UserId, null));

        // Act
        unlinked.UnlinkSavingsGoal();
        oneOff.Reschedule(Birthday, EventRecurrence.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(unlinked.RollsGoalOn).IsFalse();
            await Assert.That(oneOff.RollsGoalOn).IsFalse();
            await Assert.That(oneOff.SavingsGoalId).IsNotNull();
        }
    }

    [Test]
    public async Task MoveGoalOn_TheDayHasPassed_AimsTheGoalAtNextYearWithTheEventsTarget()
    {
        // Arrange
        SavingsGoal goal = Goal(UserId, Birthday.ToDateTime(TimeOnly.MinValue));
        CalendarEvent calendarEvent = Rolling(goal);

        // Act
        bool moved = calendarEvent.MoveGoalOn(goal, Birthday.AddDays(1));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(moved).IsTrue();
            await Assert.That(goal.TargetDate).IsEqualTo(new DateTime(2027, 3, 14));
            await Assert.That(goal.Target).IsEqualTo(100m);
        }
    }

    [Test]
    public async Task MoveGoalOn_OnTheDayOrBefore_LeavesTheGoal()
    {
        // Arrange
        SavingsGoal goal = Goal(UserId, Birthday.ToDateTime(TimeOnly.MinValue));
        CalendarEvent calendarEvent = Rolling(goal);

        // Act
        bool moved = calendarEvent.MoveGoalOn(goal, Birthday);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(moved).IsFalse();
            await Assert.That(goal.TargetDate).IsEqualTo(Birthday.ToDateTime(TimeOnly.MinValue));
        }
    }

    [Test]
    public async Task MoveGoalOn_NotRollingOn_LeavesTheGoal()
    {
        // Arrange
        SavingsGoal goal = Goal(UserId, Birthday.ToDateTime(TimeOnly.MinValue));
        CalendarEvent calendarEvent = new(UserId, "Birthday", Birthday, EventRecurrence.Yearly);
        calendarEvent.LinkSavingsGoal(goal);

        // Act
        bool moved = calendarEvent.MoveGoalOn(goal, Birthday.AddMonths(2));

        // Assert
        await Assert.That(moved).IsFalse();
    }

    [Test]
    public async Task MoveGoalOn_AnotherGoal_Throws()
    {
        // Arrange
        CalendarEvent calendarEvent = Rolling(Goal(UserId, null));

        // Act & Assert
        await Assert.That(() => calendarEvent.MoveGoalOn(Goal(UserId, null), Birthday))
            .Throws<InvalidOperationException>();
    }

    private static CalendarEvent Rolling(SavingsGoal goal)
    {
        CalendarEvent calendarEvent = new(UserId, "Birthday", Birthday, EventRecurrence.Yearly);
        calendarEvent.SetSavingsTarget(100m);
        calendarEvent.LinkSavingsGoal(goal);
        calendarEvent.SetRollsGoalOn(true);
        return calendarEvent;
    }

    private static SavingsGoal Goal(Guid userId, DateTime? targetDate) =>
        new(userId, "Birthday", 0m, 0m, 60m, targetDate);

    [Test]
    public async Task EnsureSavingsAreUnchangedFor_SomeoneElse_LeavingThemAsTheyAre_IsAllowed()
    {
        CalendarEvent calendarEvent = new(UserId, "Mum's birthday", Birthday, EventRecurrence.Yearly);
        calendarEvent.SetSavingsTarget(100m);

        await Assert.That(() => calendarEvent.EnsureSavingsAreUnchangedFor(Guid.NewGuid(), 100m, null, false, false))
            .ThrowsNothing();
    }

    [Test]
    [Arguments(150, false)]
    [Arguments(100, true)]
    public async Task EnsureSavingsAreUnchangedFor_SomeoneElse_ChangingThem_IsRefused(int target, bool newGoal)
    {
        CalendarEvent calendarEvent = new(UserId, "Mum's birthday", Birthday, EventRecurrence.Yearly);
        calendarEvent.SetSavingsTarget(100m);

        await Assert.That(() => calendarEvent.EnsureSavingsAreUnchangedFor(Guid.NewGuid(), target, null, newGoal,
            false)).Throws<DomainRuleException>();
    }

    [Test]
    public async Task EnsureSavingsAreUnchangedFor_ItsOwner_MayChangeThem()
    {
        CalendarEvent calendarEvent = new(UserId, "Mum's birthday", Birthday, EventRecurrence.Yearly);

        await Assert.That(() => calendarEvent.EnsureSavingsAreUnchangedFor(UserId, 250m, null, true, false))
            .ThrowsNothing();
    }
}
