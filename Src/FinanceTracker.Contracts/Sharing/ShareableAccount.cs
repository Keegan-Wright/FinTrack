namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// One of the signed in user's own accounts, and whether they share it with the person in a link.
/// </summary>
public sealed record ShareableAccount
{
    /// <summary>
    /// The account's id.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// The account's name, as the bank gives it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The bank it's with.
    /// </summary>
    public required string BankName { get; init; }

    /// <summary>
    /// How it's shared, null when it isn't.
    /// </summary>
    public ShareMode? Mode { get; init; }
}
