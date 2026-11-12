namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// An account of the user's and one of the other person's that look like the same joint account, the same sort code and
/// number or IBAN, with one of them shared. Merging them counts it once.
/// </summary>
public sealed record JointAccountSuggestion
{
    /// <summary>
    /// The user's copy.
    /// </summary>
    public required Guid YourAccountId { get; init; }

    /// <summary>
    /// The user's copy's name.
    /// </summary>
    public required string YourAccountName { get; init; }

    /// <summary>
    /// The other person's copy.
    /// </summary>
    public required Guid TheirAccountId { get; init; }

    /// <summary>
    /// The other person's copy's name, as their bank gives it.
    /// </summary>
    public required string TheirAccountName { get; init; }
}
