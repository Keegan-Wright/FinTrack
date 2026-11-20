namespace FinanceTracker.Contracts.Tokens;

/// <summary>
/// How making a personal access token went.
/// </summary>
public sealed class CreatedToken
{
    /// <summary>
    /// Why it wasn't made, in words for the user. Empty when it was.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// The whole token, shown to the user this once. Null when it wasn't made.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Whether it was made.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
