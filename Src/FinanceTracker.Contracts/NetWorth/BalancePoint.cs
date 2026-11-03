using System.ComponentModel;

namespace FinanceTracker.Contracts.NetWorth;

/// <summary>
/// A closing balance on one day, an account's or what a savings goal had saved.
/// </summary>
public class BalancePoint
{
    /// <summary>
    /// The day.
    /// </summary>
    [Description("The day")]
    public DateOnly Date { get; init; }

    /// <summary>
    /// The balance at the end of the day, a card's being what's owed as a negative amount.
    /// </summary>
    [Description("The balance at the end of the day")]
    public decimal Amount { get; init; }

    /// <summary>
    /// Worked out from transactions rather than synced from the bank, so approximate.
    /// </summary>
    [Description("Whether it was worked out from transactions rather than synced")]
    public bool IsDerived { get; init; }
}
