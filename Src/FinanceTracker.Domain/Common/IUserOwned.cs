namespace FinanceTracker.Domain.Common;

/// <summary>
/// Marked as a user owned entity.
/// </summary>
public interface IUserOwned
{
    Guid UserId { get; }
}
