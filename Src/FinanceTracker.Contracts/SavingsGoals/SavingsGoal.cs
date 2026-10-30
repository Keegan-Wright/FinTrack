using System.ComponentModel;

namespace FinanceTracker.Contracts.SavingsGoals;

/// <summary>
/// Something the user is saving up for, and how it's going.
/// </summary>
public class SavingsGoal
{
    /// <summary>
    /// The goal's id.
    /// </summary>
    [Description("The goal's id")]
    public required Guid Id { get; init; }

    /// <summary>
    /// The goal's name.
    /// </summary>
    [Description("The goal's name")]
    public required string Name { get; init; }

    /// <summary>
    /// How much is saved so far, worked out from <see cref="Source"/>.
    /// </summary>
    [Description("How much is saved so far")]
    public decimal Saved { get; init; }

    /// <summary>
    /// How much the user puts towards it each month.
    /// </summary>
    [Description("How much the user puts towards the goal each month")]
    public decimal MonthlyContribution { get; init; }

    /// <summary>
    /// How much the user is saving up to, 0 for no target.
    /// </summary>
    [Description("How much the user is saving up to")]
    public decimal Target { get; init; }

    /// <summary>
    /// When the user wants to reach <see cref="Target"/> by.
    /// </summary>
    [Description("When the user wants to reach the target by")]
    public DateTime? TargetDate { get; init; }

    /// <summary>
    /// Where <see cref="Saved"/> comes from.
    /// </summary>
    [Description("Where the saved amount comes from")]
    public SavingsGoalSource Source { get; init; } = SavingsGoalSource.Manual;

    /// <summary>
    /// The typed in amount for <see cref="SavingsGoalSource.Manual"/>, or what was saved before
    /// <see cref="TrackedFrom"/> for <see cref="SavingsGoalSource.Tags"/>, for editing.
    /// </summary>
    [Description("The typed in or starting amount, for editing")]
    public decimal StartingAmount { get; init; }

    /// <summary>
    /// The savings account backing it, for <see cref="SavingsGoalSource.Account"/>.
    /// </summary>
    [Description("The savings account backing the goal")]
    public Guid? AccountId { get; init; }

    /// <summary>
    /// The backing account's name, null when it's been removed.
    /// </summary>
    [Description("The backing account's name, null when it's been removed")]
    public string? AccountName { get; init; }

    /// <summary>
    /// The tags whose transactions count, for <see cref="SavingsGoalSource.Tags"/>.
    /// </summary>
    [Description("The tags whose transactions count towards the goal")]
    public List<Guid> TagIds { get; init; } = [];

    /// <summary>
    /// The first day tagged transactions count, for <see cref="SavingsGoalSource.Tags"/>.
    /// </summary>
    [Description("The first day tagged transactions count")]
    public DateOnly? TrackedFrom { get; init; }

    /// <summary>
    /// Whether it tracks tags and all of them were deleted.
    /// </summary>
    [Description("Whether all of the goal's tags were deleted")]
    public bool CoversNothing { get; init; }

    /// <summary>
    /// When the monthly contribution reaches the target from here, null without both or once it's reached.
    /// </summary>
    [Description("When the monthly contribution reaches the target")]
    public DateOnly? ReachedBy { get; init; }

    /// <summary>
    /// What it takes each month to reach the target by its date, null without both or once the date has passed.
    /// </summary>
    [Description("What it takes each month to reach the target by its date")]
    public decimal? NeededPerMonth { get; init; }

    /// <summary>
    /// Whether the monthly contribution reaches the target by its date, null when there's nothing to judge.
    /// </summary>
    [Description("Whether the monthly contribution reaches the target by its date")]
    public bool? OnTrack { get; init; }
}
