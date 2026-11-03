namespace FinanceTracker.Contracts.NetWorth;

/// <summary>
/// What a <see cref="NetWorthItem"/> is.
/// </summary>
public enum NetWorthItemKind
{
    /// <summary>
    /// A bank account.
    /// </summary>
    Account,

    /// <summary>
    /// A credit or charge card.
    /// </summary>
    Card,

    /// <summary>
    /// A debt not linked to an account.
    /// </summary>
    Debt
}
