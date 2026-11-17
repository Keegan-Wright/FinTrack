namespace FinanceTracker.Domain.Common;

/// <summary>
/// Timestamps maintained by persistence
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// When it was first saved, in UTC.
    /// </summary>
    DateTime Created { get; }
    /// <summary>
    /// When it was last changed, in UTC. Null until it's changed.
    /// </summary>
    DateTime? Updated { get; }
}
