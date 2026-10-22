namespace FinanceTracker.Domain.Common;

/// <summary>
/// Consistency boundary owned by a single user. Only aggregate roots are loaded and saved.
/// </summary>
public abstract class AggregateRoot : Entity, IUserOwned
{
    [Obsolete("EF Use Only")]
    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid userId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        UserId = userId;
    }

    public Guid UserId { get; private set; }

    public uint RowVersion { get; private set; }
}
