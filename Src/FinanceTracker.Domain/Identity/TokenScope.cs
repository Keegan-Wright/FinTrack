namespace FinanceTracker.Domain.Identity;

/// <summary>
/// What a personal access token may do.
/// </summary>
public enum TokenScope
{
    /// <summary>
    /// Read the user's data, never change it. The only scope.
    /// </summary>
    Read
}
