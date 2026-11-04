namespace FinanceTracker.Tests.Unit.Shared;

/// <summary>
/// A clock stopped at <paramref name="utcNow"/>, where the user is in <paramref name="localTimeZone"/>, the
/// machine's when omitted.
/// </summary>
public sealed class FixedTimeProvider(DateTime utcNow, TimeZoneInfo? localTimeZone = null) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

    public override TimeZoneInfo LocalTimeZone => localTimeZone ?? base.LocalTimeZone;
}
