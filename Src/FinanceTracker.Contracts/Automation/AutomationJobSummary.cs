namespace FinanceTracker.Contracts.Automation;

/// <summary>
/// How a background job is doing its schedule, when it runs next and how its recent runs went. A job that only
/// ever runs by hand, like a user's sync now, has no <see cref="Schedule"/>.
/// </summary>
/// <param name="Function">The job, see <see cref="AutomationCron.Function"/>.</param>
/// <param name="Schedule">The job's schedule and settings, or <see langword="null"/> if it only runs by hand.</param>
/// <param name="NextRunAt">When the schedule runs it next in the server's local time, or <see langword="null"/>
/// when it has no schedule or is paused.</param>
/// <param name="LastRun">The latest run that has started, or <see langword="null"/> if none has.</param>
/// <param name="SuccessRate">The percentage of the last <see cref="RecentRuns"/> finished runs that succeeded, or
/// <see langword="null"/> if none have finished.</param>
/// <param name="RecentRuns">How many finished runs <see cref="SuccessRate"/> covers, up to
/// <see cref="SuccessRateRuns"/>.</param>
public record AutomationJobSummary(
    string Function,
    AutomationCron? Schedule,
    DateTime? NextRunAt,
    AutomationRun? LastRun,
    int? SuccessRate,
    int RecentRuns)
{
    /// <summary>
    /// How many of a job's latest finished runs <see cref="SuccessRate"/> looks at.
    /// </summary>
    public const int SuccessRateRuns = 20;
}
