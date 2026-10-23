namespace FinanceTracker.Contracts.Auth;

/// <summary>
/// The roles a user can hold. Every signed in user can use their own data, roles only grant app wide powers.
/// </summary>
public static class FinTrackRoles
{
    /// <summary>
    /// Allowed to edit the background job schedules, which apply to every user. Held by the first user to
    /// register.
    /// </summary>
    public const string Admin = "Admin";
}
