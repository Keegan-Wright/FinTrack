namespace FinanceTracker.Ai.Contracts;

/// <summary>
/// Turns AI on or off for the signed in user.
/// </summary>
public sealed class ChangeAiOptIn
{
    /// <summary>
    /// Whether they want AI on.
    /// </summary>
    public bool OptedIn { get; set; }
}
