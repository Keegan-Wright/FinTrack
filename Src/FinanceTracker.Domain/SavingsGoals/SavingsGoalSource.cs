namespace FinanceTracker.Domain.SavingsGoals;

/// <summary>
/// Where a <see cref="SavingsGoal"/>'s saved amount comes from.
/// </summary>
public enum SavingsGoalSource
{
    /// <summary>
    /// The amount the user types in.
    /// </summary>
    Manual = 1,

    /// <summary>
    /// A savings account's current balance, the account backing no other goal.
    /// </summary>
    Account = 2,

    /// <summary>
    /// A starting amount plus the tagged money in, less the tagged money out, from a day on.
    /// </summary>
    Tags = 3
}
