using Quartz;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

/// <summary>
/// A job that does nothing but count its runs, for the automation service's scheduler.
/// </summary>
public sealed class RecordingJob : IJob
{
    private static int _runs;

    public static int Runs => _runs;

    public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _runs);
        return ValueTask.CompletedTask;
    }
}
