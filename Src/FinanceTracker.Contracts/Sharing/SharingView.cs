namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// How the signed in user sees what's shared with them in the money views.
/// </summary>
public sealed record SharingView
{
    /// <summary>
    /// Accounts shared with them show beside their own, Everyone, rather than only their own, Just mine.
    /// </summary>
    public required bool IncludesShared { get; init; }

    /// <summary>
    /// Anyone shares an account with them now, so the choice means something. When no one does there's nothing to
    /// switch.
    /// </summary>
    public required bool HasSharedAccounts { get; init; }
}
