using System.ComponentModel;

namespace FinanceTracker.Contracts.SavingsGoals;

/// <summary>
/// One of the user's accounts that could back a savings goal, any but a card.
/// </summary>
public class SavingsGoalAccount
{
    /// <summary>
    /// The account's id.
    /// </summary>
    [Description("The account's id")]
    public Guid AccountId { get; init; }

    /// <summary>
    /// The account's name.
    /// </summary>
    [Description("The account's name")]
    public required string Name { get; init; }

    /// <summary>
    /// The goal it already backs, null when it's free. An account backs one goal at most.
    /// </summary>
    [Description("The goal the account already backs, null when it's free")]
    public Guid? GoalId { get; init; }
}
