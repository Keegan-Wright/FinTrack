namespace FinanceTracker.Infrastructure.Sharing;

/// <summary>
/// Keeps the user's joint accounts filled in while the shown copy's connection has lapsed, see
/// <see cref="Domain.Sharing.JointAccount.Gaps"/>. Run after their connections sync.
/// </summary>
public interface IJointAccountGapService
{
    /// <summary>
    /// Opens or closes the gap of each joint account the user has a copy of, marks the hidden copy's transactions
    /// that fill it, and matches transfers again around those that changed.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken);
}
