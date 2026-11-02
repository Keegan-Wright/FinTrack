using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.SavingsGoals;

namespace FinanceTracker.Domain.Calendar;

/// <summary>
/// Something the user puts on their calendar to plan saving around, optionally
/// with what they want saved for it in a <see cref="SavingsGoal"/>.
/// </summary>
/// <remarks>
/// A yearly event on 29 February falls on the 28th in other years. One that <see cref="RollsGoalOn"/> moves its goal
/// on to its next date once each one passes, see <see cref="MoveGoalOn"/>.
/// </remarks>
public sealed class CalendarEvent : AggregateRoot
{
    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private CalendarEvent()
    {
    }
#pragma warning restore CS8618

    /// <exception cref="ArgumentException">The title is blank.</exception>
    public CalendarEvent(Guid userId, string title, DateOnly date, EventRecurrence recurrence) : base(userId)
    {
        Rename(title);
        Reschedule(date, recurrence);
    }

    public string Title { get; private set; }

    /// <summary>
    /// The day it's on, or first on for a yearly one.
    /// </summary>
    public DateOnly Date { get; private set; }

    public EventRecurrence Recurrence { get; private set; }

    /// <summary>
    /// Anything the user wants to remember, null for none.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// What the user wants saved for it each time, null for nothing.
    /// </summary>
    public decimal? SavingsTarget { get; private set; }

    /// <summary>
    /// The goal saving for it, null for none.
    /// </summary>
    public Guid? SavingsGoalId { get; private set; }

    /// <summary>
    /// Whether its goal moves on to its next date once each passes. Only a yearly event with a goal does.
    /// </summary>
    public bool RollsGoalOn { get; private set; }

    [MemberNotNull(nameof(Title))]
    public void Rename(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
    }

    /// <summary>
    /// Moves it to <paramref name="date"/>. A one-off event's goal stays where it is.
    /// </summary>
    public void Reschedule(DateOnly date, EventRecurrence recurrence)
    {
        Date = date;
        Recurrence = recurrence;
        if (recurrence != EventRecurrence.Yearly)
        {
            RollsGoalOn = false;
        }
    }

    public void ChangeNotes(string? notes) => Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    /// <exception cref="ArgumentOutOfRangeException">The target isn't more than 0.</exception>
    public void SetSavingsTarget(decimal? savingsTarget)
    {
        if (savingsTarget is { } target)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(target);
        }

        SavingsTarget = savingsTarget;
    }

    /// <summary>
    /// Saves for it in <paramref name="goal"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The goal belongs to another user.</exception>
    public void LinkSavingsGoal(SavingsGoal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        if (goal.UserId != UserId)
        {
            throw new InvalidOperationException("An event can only save in the user's own goals.");
        }

        SavingsGoalId = goal.Id;
    }

    /// <summary>
    /// Stops saving for it in a goal, which stops moving one on too.
    /// </summary>
    public void UnlinkSavingsGoal()
    {
        SavingsGoalId = null;
        RollsGoalOn = false;
    }

    /// <exception cref="InvalidOperationException">Rolling on is asked for an event that isn't yearly or has no goal.
    /// </exception>
    public void SetRollsGoalOn(bool rollsGoalOn)
    {
        if (rollsGoalOn && (Recurrence != EventRecurrence.Yearly || SavingsGoalId is null))
        {
            throw new InvalidOperationException("Only a yearly event with a savings goal can move its goal on.");
        }

        RollsGoalOn = rollsGoalOn;
    }

    /// <summary>
    /// The day it falls on in <paramref name="year"/>, whether or not it happens then. 29 February is the 28th in a
    /// year that isn't a leap year.
    /// </summary>
    public DateOnly OnIn(int year)
    {
        int day = Math.Min(Date.Day, DateTime.DaysInMonth(year, Date.Month));
        return new DateOnly(year, Date.Month, day);
    }

    public bool OccursOn(DateOnly day)
    {
        if (Recurrence != EventRecurrence.Yearly)
        {
            return day == Date;
        }

        return day >= Date && OnIn(day.Year) == day;
    }

    /// <summary>
    /// The first day it's on from <paramref name="from"/>, that day included. Null for a one-off that's passed.
    /// </summary>
    public DateOnly? NextOn(DateOnly from)
    {
        if (Recurrence != EventRecurrence.Yearly)
        {
            return Date >= from ? Date : null;
        }

        if (Date >= from)
        {
            return Date;
        }

        DateOnly thisYear = OnIn(from.Year);
        return thisYear >= from ? thisYear : OnIn(from.Year + 1);
    }

    /// <summary>
    /// Once the day <paramref name="goal"/> was saving for has passed, aims it at the next one, with
    /// <see cref="SavingsTarget"/> as its target when there is one. Does nothing unless it <see cref="RollsGoalOn"/>.
    /// </summary>
    /// <returns>Whether the goal moved on.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="goal"/> isn't this event's.</exception>
    public bool MoveGoalOn(SavingsGoal goal, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(goal);
        if (goal.Id != SavingsGoalId)
        {
            throw new InvalidOperationException("Only the event's own goal can be moved on.");
        }

        if (!RollsGoalOn)
        {
            return false;
        }

        if (goal.TargetDate is { } targetDate && DateOnly.FromDateTime(targetDate) >= today)
        {
            return false;
        }

        DateOnly next = NextOn(today)!.Value;
        goal.SetTarget(SavingsTarget ?? goal.Target, next.ToDateTime(TimeOnly.MinValue));
        return true;
    }
}
