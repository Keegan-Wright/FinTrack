namespace FinanceTracker.Domain.Common;

/// <summary>
/// Timestamps maintained by persistence
/// </summary>
public interface IAuditable
{
    DateTime Created { get; }
    DateTime? Updated { get; }
}
