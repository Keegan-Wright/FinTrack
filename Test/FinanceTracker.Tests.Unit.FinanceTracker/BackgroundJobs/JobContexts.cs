using FinanceTracker.Infrastructure.Automation;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

/// <summary>
/// What Quartz hands a job as it runs it.
/// </summary>
internal static class JobContexts
{
    public static IJobExecutionContext Scheduled() => With([]);

    /// <summary>
    /// A user's sync now, see <see cref="JobNames.UserIdKey"/>.
    /// </summary>
    public static IJobExecutionContext ForUser(Guid userId) =>
        With(new JobDataMap { { JobNames.UserIdKey, userId.ToString() } });

    private static IJobExecutionContext With(JobDataMap data)
    {
        IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(data);
        return context;
    }
}
