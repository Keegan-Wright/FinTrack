namespace FinanceTracker.Tests.Unit.Shared;

/// <summary>
/// A clock stopped at <paramref name="utcNow"/>.
/// </summary>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
}
