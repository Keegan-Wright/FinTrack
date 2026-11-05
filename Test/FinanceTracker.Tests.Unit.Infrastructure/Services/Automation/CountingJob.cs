using System.Collections.Concurrent;
using Quartz;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

/// <summary>
/// A job that counts its runs by scheduler, so tests running side by side on their own schedulers don't see each
/// other's.
/// </summary>
public sealed class CountingJob : IJob
{
    private static readonly ConcurrentDictionary<string, int> RunsByScheduler = new();

    public static int Runs(string scheduler) => RunsByScheduler.GetValueOrDefault(scheduler);

    public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        RunsByScheduler.AddOrUpdate(context.Scheduler.SchedulerName, 1, static (_, runs) => runs + 1);
        return ValueTask.CompletedTask;
    }
}
