using Quartz;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

/// <summary>
/// A job that always fails, to see what's still told about it.
/// </summary>
public sealed class FailingJob : IJob
{
    public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The bank didn't answer");
}
