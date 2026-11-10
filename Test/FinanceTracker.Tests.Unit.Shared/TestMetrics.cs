using System.Diagnostics.Metrics;
using FinanceTracker.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace FinanceTracker.Tests.Unit.Shared;

/// <summary>
/// A <see cref="FinTrackMetrics"/> on a meter of the test's own, so what it records isn't mixed with the tests
/// running alongside. <see cref="Collect{T}"/> before the work to see what it recorded.
/// </summary>
public sealed class TestMetrics : IDisposable
{
    private readonly ServiceProvider _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();

    /// <param name="timeProvider">The clock the sync age is worked out by, the real one unless given.</param>
    public TestMetrics(TimeProvider? timeProvider = null) =>
        Metrics = new FinTrackMetrics(MeterFactory, timeProvider ?? TimeProvider.System);

    /// <summary>
    /// For tests that need one but don't look at it. Nothing listens to its meter.
    /// </summary>
    public static FinTrackMetrics Unobserved { get; } = new TestMetrics().Metrics;

    public FinTrackMetrics Metrics { get; }

    private IMeterFactory MeterFactory => _provider.GetRequiredService<IMeterFactory>();

    /// <summary>
    /// Collects what's recorded on the instrument named <paramref name="instrument"/>, one of the
    /// <see cref="FinTrackMetrics"/> names, from now on.
    /// </summary>
    public MetricCollector<T> Collect<T>(string instrument) where T : struct =>
        new(MeterFactory, FinTrackMetrics.MeterName, instrument);

    public void Dispose() => _provider.Dispose();
}
