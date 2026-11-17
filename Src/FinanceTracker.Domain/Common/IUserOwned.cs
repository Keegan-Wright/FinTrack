namespace FinanceTracker.Domain.Common;

/// <summary>
/// Marked as a user owned entity.
/// </summary>
public interface IUserOwned
{
    /// <summary>
    /// The user who owns it.
    /// </summary>
    Guid UserId { get; }
}
