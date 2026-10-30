namespace FinanceTracker.Contracts.SavingsGoals;

/// <summary>
/// Something the user is saving up for.
/// </summary>
public class SavingsGoal
{
    /// <summary>
    /// The goal's id.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The goal's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// How much is saved so far.
    /// </summary>
    public decimal Saved { get; init; }

    /// <summary>
    /// How much the user puts towards it each month.
    /// </summary>
    public decimal MonthlyContribution { get; init; }

    /// <summary>
    /// How much the user is saving up to, 0 for no target.
    /// </summary>
    public decimal Target { get; init; }

    /// <summary>
    /// When the user wants to reach <see cref="Target"/> by.
    /// </summary>
    public DateTime? TargetDate { get; init; }
}
