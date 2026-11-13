namespace FinanceTracker.Middleware;

/// <summary>
/// What a request does that's limited in how often, see <see cref="RateLimitedActions"/>.
/// </summary>
public enum RateLimitedAction
{
    /// <summary>
    /// Posting the sign in, register or a password reset form, limited per address against guessing.
    /// </summary>
    SignIn,

    /// <summary>
    /// Inviting someone to share, limited per user as it also says whether an account exists.
    /// </summary>
    Invite
}
