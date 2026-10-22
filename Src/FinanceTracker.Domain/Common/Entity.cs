namespace FinanceTracker.Domain.Common;

/// <summary>
/// Base for every persisted domain object. Identity is assigned on construction so children can be
/// attached to an aggregate before it is saved.
/// </summary>
public abstract class Entity : IAuditable, IEquatable<Entity>
{
    protected Entity() => Id = Guid.CreateVersion7();

    public Guid Id { get; init; }
    public DateTime Created { get; private set; }
    public DateTime? Updated { get; private set; }

    public bool Equals(Entity? other) =>
        other is not null && (ReferenceEquals(this, other) || (GetType() == other.GetType() && Id == other.Id));

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
