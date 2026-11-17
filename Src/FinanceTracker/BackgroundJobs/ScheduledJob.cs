namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// A job that runs on a schedule, and the schedule it starts with, see <see cref="ScheduledJobs"/>.
/// </summary>
/// <param name="Function">The job's name, as Quartz knows it.</param>
/// <param name="Description">What it does, shown on the automation page.</param>
/// <param name="Expression">A Quartz cron expression in UTC.</param>
/// <param name="Retries">How many times a failed run is retried.</param>
/// <param name="RetryIntervals">Seconds before each retry, the last used for any after it.</param>
public sealed record ScheduledJob(
    string Function,
    string Description,
    string Expression,
    int Retries,
    IReadOnlyList<int> RetryIntervals);
