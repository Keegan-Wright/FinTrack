namespace FinanceTracker.Domain.Common;

/// <summary>
/// Consistency boundary owned by a single user. Only aggregate roots are loaded and saved.
/// </summary>
public abstract class AggregateRoot : Entity, IUserOwned
{
    /// <summary>
    /// Only for EF Core, which sets the owner when it loads one.
    /// </summary>
    [Obsolete("EF Use Only")]
    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Makes one owned by <paramref name="userId"/>.
    /// </summary>
    /// <param name="userId">The user who owns it.</param>
    protected AggregateRoot(Guid userId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        UserId = userId;
    }

    /// <inheritdoc/>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Changes each time it's saved. A save that started from an older version fails rather than overwriting.
    /// </summary>
    public uint RowVersion { get; private set; }
}
