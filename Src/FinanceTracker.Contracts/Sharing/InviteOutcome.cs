namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// What came of an invite to share the link waiting for their answer, or why it wasn't sent.
/// </summary>
public sealed record InviteOutcome
{
    /// <summary>
    /// The invite as sent, null when it was refused.
    /// </summary>
    public UserLink? Link { get; init; }

    /// <summary>
    /// Why it wasn't sent, null when it was.
    /// </summary>
    public InviteRefusal? Refusal { get; init; }

    /// <summary>
    /// The invite went, waiting for their answer as <paramref name="link"/>.
    /// </summary>
    public static InviteOutcome Sent(UserLink link) => new() { Link = link };

    /// <summary>
    /// The invite didn't go, for <paramref name="refusal"/>.
    /// </summary>
    public static InviteOutcome Refused(InviteRefusal refusal) => new() { Refusal = refusal };
}
