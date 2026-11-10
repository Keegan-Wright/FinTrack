using System.Collections.Concurrent;
using System.Diagnostics;

namespace FinanceTracker.Tests.Unit.Infrastructure;

/// <summary>
/// Records the services spans under a root the test starts, so spans from tests running alongside are left out.
/// </summary>
public sealed class SpanRecorder : IDisposable
{
    private static readonly ActivitySource TestSource = new(nameof(SpanRecorder));

    private readonly ConcurrentBag<Activity> _stopped = [];
    private readonly ActivityListener _listener;
    private ActivityTraceId _traceId;

    public SpanRecorder()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = static x => x.Name is "FinanceTracker.Infrastructure" or nameof(SpanRecorder),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == _traceId)
                {
                    _stopped.Add(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>
    /// The span the test's work runs under, as a sync job's <c>Sync connection</c> span.
    /// </summary>
    public Activity Root { get; private set; } = null!;

    /// <summary>
    /// The names of the spans recorded under <see cref="Root"/>.
    /// </summary>
    public IEnumerable<string> Names => _stopped.Select(static x => x.OperationName);

    public Activity? StartRoot()
    {
        Root = TestSource.StartActivity("root")!;
        _traceId = Root.TraceId;
        return Root;
    }

    /// <summary>
    /// The one recorded span named <paramref name="name"/>.
    /// </summary>
    public Activity Named(string name) => _stopped.Single(x => x.OperationName == name);

    public void Dispose() => _listener.Dispose();
}
