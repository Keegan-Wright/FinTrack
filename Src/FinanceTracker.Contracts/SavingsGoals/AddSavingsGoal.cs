using System.ComponentModel;

namespace FinanceTracker.Contracts.SavingsGoals;

/// <summary>
/// A new savings goal for the user.
/// </summary>
public class AddSavingsGoal
{
    /// <summary>
    /// The goal's name.
    /// </summary>
    [Description("Name of the savings goal")]
    public required string Name { get; init; }

    /// <summary>
    /// How much is saved so far.
    /// </summary>
    [Description("How much is saved towards the goal so far")]
    public required decimal Saved { get; init; }

    /// <summary>
    /// How much the user puts towards it each month.
    /// </summary>
    [Description("How much the user puts towards the goal each month")]
    public required decimal MonthlyContribution { get; init; }

    /// <summary>
    /// How much the user is saving up to, 0 for no target.
    /// </summary>
    [Description("How much the user is saving up to")]
    public required decimal Target { get; init; }

    /// <summary>
    /// When the user wants to reach <see cref="Target"/> by.
    /// </summary>
    [Description("When the user wants to reach the target by")]
    public required DateTime? TargetDate { get; init; }
}
