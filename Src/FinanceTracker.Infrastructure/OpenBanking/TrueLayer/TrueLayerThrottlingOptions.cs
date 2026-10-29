namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer;

/// <summary>
/// How fast calls to TrueLayer's data API can go, so a sync doesn't send them in bursts TrueLayer's rate limiter
/// refuses. Applies across the whole app, every user's connections share TrueLayer's limit. Calls over either limit
/// wait their turn. Bound from <see cref="SectionName"/>.
/// </summary>
public sealed class TrueLayerThrottlingOptions
{
    public const string SectionName = "OpenBanking:Throttling";

    /// <summary>
    /// How many calls can be in flight at once, 0 for no limit.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 2;

    /// <summary>
    /// How many calls can start each second, 0 for no limit.
    /// </summary>
    public int MaxCallsPerSecond { get; set; } = 4;
}
