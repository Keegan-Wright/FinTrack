using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// A savings goal a transaction counts towards.
/// </summary>
public class TransactionSavingsGoal
{
    /// <summary>
    /// The goal's id.
    /// </summary>
    [Description("The savings goal's id")]
    public Guid SavingsGoalId { get; init; }

    /// <summary>
    /// The goal's name.
    /// </summary>
    [Description("The savings goal's name")]
    public required string Name { get; init; }
}
