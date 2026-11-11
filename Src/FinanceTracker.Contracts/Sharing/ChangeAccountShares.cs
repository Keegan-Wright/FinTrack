namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// The accounts the user shares in a link from now on. Any of their accounts left out stops being shared, so this is
/// the whole choice, not a change to it.
/// </summary>
public sealed record ChangeAccountShares
{
    /// <summary>
    /// The accounts to share and how, none to share nothing.
    /// </summary>
    public required IReadOnlyList<AccountShareChoice> Accounts { get; init; }
}
