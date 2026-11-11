namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// What's shared both ways in one link, each of the signed in user's accounts and whether they share it, and the
/// accounts the other person shares with them.
/// </summary>
public sealed record LinkShares
{
    /// <summary>
    /// The link's id.
    /// </summary>
    public required Guid LinkId { get; init; }

    /// <summary>
    /// The other person's first and last name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Every account of the user's own, shared or not, so they can choose.
    /// </summary>
    public required IReadOnlyList<ShareableAccount> YourAccounts { get; init; }

    /// <summary>
    /// The accounts the other person shares with them.
    /// </summary>
    public required IReadOnlyList<AccountSharedWithYou> TheirAccounts { get; init; }
}
