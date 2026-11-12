namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// Every one of the user's things besides accounts they want shared in a link. Anything they shared before that isn't
/// in it stops being shared. Validated by <see cref="Validation.ChangeItemSharesValidator"/>.
/// </summary>
public sealed record ChangeItemShares
{
    /// <summary>
    /// The things to share and how, empty to share none.
    /// </summary>
    public required IReadOnlyList<ItemShareChoice> Items { get; init; }
}
