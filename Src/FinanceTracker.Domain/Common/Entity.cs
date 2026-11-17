namespace FinanceTracker.Domain.Common;

/// <summary>
/// Base for every persisted domain object. Identity is assigned on construction so children can be
/// attached to an aggregate before it is saved.
/// </summary>
public abstract class Entity : IAuditable, IEquatable<Entity>
{
    /// <summary>
    /// Gives it a new id, ordered by when it was made.
    /// </summary>
    protected Entity() => Id = Guid.CreateVersion7();

    /// <summary>
    /// What identifies it. Two entities with the same id are the same.
    /// </summary>
    public Guid Id { get; init; }
    /// <inheritdoc/>
    public DateTime Created { get; private set; }
    /// <inheritdoc/>
    public DateTime? Updated { get; private set; }

    /// <summary>
    /// Whether <paramref name="other"/> is the same entity, the same type with the same id.
    /// </summary>
    /// <param name="other">The entity to compare with.</param>
    public bool Equals(Entity? other) =>
        other is not null && (ReferenceEquals(this, other) || (GetType() == other.GetType() && Id == other.Id));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// Whether both are the same entity, see <see cref="Equals(Entity)"/>.
    /// </summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    /// <summary>
    /// Whether they're different entities, see <see cref="Equals(Entity)"/>.
    /// </summary>
    /// <param name="left">The first entity.</param>
    /// <param name="right">The second entity.</param>
    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
