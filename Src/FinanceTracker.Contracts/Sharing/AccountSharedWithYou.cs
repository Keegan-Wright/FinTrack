namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// One of the other person's accounts they share with the signed in user.
/// </summary>
public sealed record AccountSharedWithYou
{
    /// <summary>
    /// The account's name, as their bank gives it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The bank it's with.
    /// </summary>
    public required string BankName { get; init; }

    /// <summary>
    /// What the user can do with it.
    /// </summary>
    public required ShareMode Mode { get; init; }
}
