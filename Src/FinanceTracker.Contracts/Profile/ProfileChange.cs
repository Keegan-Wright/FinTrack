namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// How a change to the user's own account went.
/// </summary>
public sealed class ProfileChange
{
    /// <summary>
    /// Why it wasn't made, in words for the user, e.g. the current password was wrong. Empty when it was.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// For an email change, whether a link was sent to the new email to confirm it, rather than it changing
    /// straight away as it does without a mail server.
    /// </summary>
    public bool LinkSent { get; init; }

    /// <summary>
    /// Whether the change was made, or the link to make it sent.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
