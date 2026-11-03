using System.ComponentModel;

namespace FinanceTracker.Contracts.NetWorth;

/// <summary>
/// One account, card or debt in the latest net worth.
/// </summary>
public class NetWorthItem
{
    /// <summary>
    /// Its name.
    /// </summary>
    [Description("The account, card or debt's name")]
    public required string Name { get; init; }

    /// <summary>
    /// What it is.
    /// </summary>
    [Description("Whether it's an account, card or debt")]
    public NetWorthItemKind Kind { get; init; }

    /// <summary>
    /// What it adds, negative for what's owed.
    /// </summary>
    [Description("What it adds to net worth, negative for what's owed")]
    public decimal Amount { get; init; }
}
