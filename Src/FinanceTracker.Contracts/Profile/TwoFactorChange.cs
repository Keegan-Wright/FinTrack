namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// How a change to the user's two-factor sign in went.
/// </summary>
public sealed class TwoFactorChange
{
    /// <summary>
    /// Why it wasn't made, in words for the user. Empty when it was.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// New recovery codes, shown to the user once. Empty when the change made none.
    /// </summary>
    public IReadOnlyList<string> RecoveryCodes { get; init; } = [];

    /// <summary>
    /// Whether the change was made.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
