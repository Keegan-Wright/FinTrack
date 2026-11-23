namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// A link to confirm the new email was sent to it. The email changes once it's followed, rather than straight away
/// as it does without a mail server.
/// </summary>
public sealed record EmailChangeLinkSent : ProfileChange;
