using System.ComponentModel;

namespace FinanceTracker.Contracts.SavingsGoals;

/// <summary>
/// A new savings goal, or everything about an existing one. Validated by
/// <see cref="Validation.SaveSavingsGoalValidator"/>.
/// </summary>
public class SaveSavingsGoal
{
    /// <summary>
    /// The goal's name.
    /// </summary>
    [Description("Name of the savings goal")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// How much the user is saving up to, 0 for no target.
    /// </summary>
    [Description("How much the user is saving up to")]
    public decimal Target { get; set; }

    /// <summary>
    /// When the user wants to reach <see cref="Target"/> by.
    /// </summary>
    [Description("When the user wants to reach the target by")]
    public DateTime? TargetDate { get; set; }

    /// <summary>
    /// How much the user puts towards it each month.
    /// </summary>
    [Description("How much the user puts towards the goal each month")]
    public decimal MonthlyContribution { get; set; }

    /// <summary>
    /// Where the saved amount comes from.
    /// </summary>
    [Description("Where the saved amount comes from")]
    public SavingsGoalSource Source { get; set; } = SavingsGoalSource.Manual;

    /// <summary>
    /// What's saved for <see cref="SavingsGoalSource.Manual"/>, or what was saved before <see cref="TrackedFrom"/> for
    /// <see cref="SavingsGoalSource.Tags"/>. Unused for an account.
    /// </summary>
    [Description("What's saved, or what was saved before tracking tags")]
    public decimal Saved { get; set; }

    /// <summary>
    /// The savings account backing it, for <see cref="SavingsGoalSource.Account"/>.
    /// </summary>
    [Description("The savings account backing the goal")]
    public Guid? AccountId { get; set; }

    /// <summary>
    /// The tags whose transactions count, for <see cref="SavingsGoalSource.Tags"/>.
    /// </summary>
    [Description("The tags whose transactions count towards the goal")]
    public List<Guid> TagIds { get; set; } = [];

    /// <summary>
    /// The first day tagged transactions count, for <see cref="SavingsGoalSource.Tags"/>.
    /// </summary>
    [Description("The first day tagged transactions count")]
    public DateOnly? TrackedFrom { get; set; }
}
