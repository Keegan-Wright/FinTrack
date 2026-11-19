namespace FinanceTracker.Contracts.Users;

/// <summary>
/// How giving someone the admin role, or taking it away, went.
/// </summary>
public enum AdminChange
{
    /// <summary>
    /// Done, or they were already as asked.
    /// </summary>
    Changed = 0,

    /// <summary>
    /// Refused, they sign in through the identity provider, whose admin group decides.
    /// </summary>
    FromIdentityProvider = 1,

    /// <summary>
    /// Refused, they're the last admin, and password sign in is on so nobody else could become one.
    /// </summary>
    LastAdmin = 2
}
