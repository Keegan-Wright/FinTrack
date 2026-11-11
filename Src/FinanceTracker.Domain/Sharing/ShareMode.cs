namespace FinanceTracker.Domain.Sharing;

/// <summary>
/// What the person shared with can do with it.
/// </summary>
public enum ShareMode
{
    /// <summary>
    /// See it, nothing more.
    /// </summary>
    ReadOnly = 0,

    /// <summary>
    /// See it and change it, such as tagging or splitting a shared account's transactions.
    /// </summary>
    AllowChanges = 1
}
