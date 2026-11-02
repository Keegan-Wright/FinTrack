using System.ComponentModel;

namespace FinanceTracker.Contracts.Debts;

/// <summary>
/// What a debt owed on a day.
/// </summary>
public class DebtBalancePoint
{
    /// <summary>
    /// The day.
    /// </summary>
    [Description("The day")]
    public DateOnly On { get; init; }

    /// <summary>
    /// What was owed.
    /// </summary>
    [Description("What was owed that day")]
    public decimal Owed { get; init; }
}
