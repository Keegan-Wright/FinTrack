using Quartz;

namespace FinanceTracker.Infrastructure.Automation;

/// <summary>
/// The Quartz trigger that runs a job on its schedule, built the same way when it's first added and each time it's
/// changed. Cron is in UTC, a run that's still going when the next is due skips that one, and a missed run while the
/// app was down isn't caught up.
/// </summary>
public static class JobSchedules
{
    /// <summary>
    /// The wait before a retry when none is set.
    /// </summary>
    public static readonly TimeSpan DefaultRetryWait = TimeSpan.FromSeconds(30);

    public static TriggerKey KeyFor(string function) => new(function, JobNames.ScheduleGroup);

    /// <param name="expression">A Quartz cron expression, see <see cref="IsReadable"/>.</param>
    /// <param name="retries">How many times a failed run is retried.</param>
    /// <param name="retryIntervals">Seconds before each retry, the last used for any after it.</param>
    public static ITrigger Build(string function, string description, string expression, int retries,
        IReadOnlyList<int> retryIntervals)
    {
        TriggerBuilder<IJob> trigger = TriggerBuilder.Create();
        Configure(trigger, function, description, expression, retries, retryIntervals);
        return trigger.Build();
    }

    /// <summary>
    /// Sets up <paramref name="trigger"/> as the schedule, for a trigger Quartz builds itself, e.g. one declared in
    /// its configuration. See <see cref="Build"/>.
    /// </summary>
    public static void Configure(ITriggerConfigurator<IJob> trigger, string function, string description,
        string expression, int retries, IReadOnlyList<int> retryIntervals)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(retryIntervals);

        trigger.WithIdentity(KeyFor(function))
            .ForJob(new JobKey(function))
            .WithDescription(description)
            .WithCronSchedule(expression, static cron => cron
                .InTimeZone(TimeZoneInfo.Utc)
                .WithMisfireInstruction(CronTriggerMisfireInstruction.DoNothing))
            .WithOverlapPolicy(OverlapPolicy.Skip)
            .WithRetryPolicy(RetryPolicyFor(retries, retryIntervals));
    }

    /// <summary>
    /// Whether Quartz can read <paramref name="expression"/>.
    /// </summary>
    public static bool IsReadable(string expression) => CronExpression.TryParse(expression, out _);

    /// <summary>
    /// The seconds before each retry, one per retry.
    /// </summary>
    public static int[] RetryIntervals(RetryPolicy? policy)
    {
        if (policy is null || policy.MaxAttempts == 0)
        {
            return [];
        }

        return Enumerable.Range(1, policy.MaxAttempts)
            .Select(attempt => (int)policy.DelayFor(attempt).TotalSeconds)
            .ToArray();
    }

    private static RetryPolicy RetryPolicyFor(int retries, IReadOnlyList<int> retryIntervals)
    {
        if (retries <= 0)
        {
            return RetryPolicy.None;
        }

        List<TimeSpan> delays = [];
        for (int retry = 0; retry < retries; retry++)
        {
            delays.Add(WaitBefore(retry, retryIntervals));
        }

        return RetryPolicy.Explicit(delays);
    }

    private static TimeSpan WaitBefore(int retry, IReadOnlyList<int> retryIntervals)
    {
        if (retryIntervals.Count == 0)
        {
            return DefaultRetryWait;
        }

        return TimeSpan.FromSeconds(retryIntervals[Math.Min(retry, retryIntervals.Count - 1)]);
    }
}
