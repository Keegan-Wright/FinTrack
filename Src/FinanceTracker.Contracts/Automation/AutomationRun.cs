namespace FinanceTracker.Contracts.Automation;

/// <summary>
/// One run of a background job, on its schedule or started by hand. Times are the server's local time.
/// </summary>
/// <param name="Id">The run's id in the history.</param>
/// <param name="Function">The job that ran, see <see cref="AutomationCron.Function"/>.</param>
/// <param name="Status">Where the run has got to.</param>
/// <param name="IsManual">Started by hand (a sync now, or run now) rather than by the job's schedule.</param>
/// <param name="DueAt">When the run was due to start.</param>
/// <param name="StartedAt">When it last started, or <see langword="null"/> if it hasn't yet.</param>
/// <param name="Duration">How long it ran for, or <see langword="null"/> if it hasn't finished.</param>
/// <param name="RetryCount">How many times it was retried after failing.</param>
/// <param name="Error">The error it failed with, or why it was skipped.</param>
public record AutomationRun(
    string Id,
    string Function,
    AutomationRunStatus Status,
    bool IsManual,
    DateTime DueAt,
    DateTime? StartedAt,
    TimeSpan? Duration,
    int RetryCount,
    string? Error);
