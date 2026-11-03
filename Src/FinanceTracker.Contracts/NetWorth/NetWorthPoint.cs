using System.ComponentModel;

namespace FinanceTracker.Contracts.NetWorth;

/// <summary>
/// The user's net worth at the end of one day.
/// </summary>
public class NetWorthPoint
{
    /// <summary>
    /// The day.
    /// </summary>
    [Description("The day")]
    public DateOnly Date { get; init; }

    /// <summary>
    /// The accounts in credit.
    /// </summary>
    [Description("The accounts in credit")]
    public decimal Assets { get; init; }

    /// <summary>
    /// What's owed as a positive amount: overdrawn accounts, cards and debts.
    /// </summary>
    [Description("What's owed as a positive amount")]
    public decimal Liabilities { get; init; }

    /// <summary>
    /// Assets less liabilities.
    /// </summary>
    [Description("Assets less liabilities")]
    public decimal NetWorth { get; init; }

    /// <summary>
    /// At least one balance was worked out from transactions, so it's approximate.
    /// </summary>
    [Description("Whether any balance was worked out from transactions")]
    public bool IsDerived { get; init; }

    /// <summary>
    /// At least one balance couldn't be synced that day, so it's the last one stored.
    /// </summary>
    [Description("Whether any balance couldn't be synced that day")]
    public bool IsStale { get; init; }
}
