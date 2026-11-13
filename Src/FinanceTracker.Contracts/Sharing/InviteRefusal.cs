namespace FinanceTracker.Contracts.Sharing;

/// <summary>
/// Why an invite to share wasn't sent.
/// </summary>
public enum InviteRefusal
{
    /// <summary>
    /// No user on the instance has that username or email address.
    /// </summary>
    NoSuchUser = 0,

    /// <summary>
    /// It's the user themselves.
    /// </summary>
    Yourself = 1,

    /// <summary>
    /// They're already linked with that person.
    /// </summary>
    AlreadyLinked = 2,

    /// <summary>
    /// An invite between them is already waiting for an answer, from either of them.
    /// </summary>
    AlreadyInvited = 3
}
