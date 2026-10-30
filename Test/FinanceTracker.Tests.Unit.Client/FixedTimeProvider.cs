namespace FinanceTracker.Tests.Unit.Client;

/// <summary>
/// A clock stopped at <paramref name="utcNow"/>, for pages that work out the current month.
/// </summary>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
}
