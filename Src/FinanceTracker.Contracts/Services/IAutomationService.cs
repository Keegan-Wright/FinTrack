using FinanceTracker.Contracts.Automation;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The scheduled background jobs and the signed in user's syncs.
/// </summary>
public interface IAutomationService
{
    /// <summary>
    /// Every scheduled (cron) job. Jobs are deployment wide.
    /// </summary>
    IAsyncEnumerable<AutomationCron> GetJobsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Changes a scheduled job's schedule, retries and whether it runs. Validate the request with
    /// <see cref="Validation.CronJobUpdateRequestValidator"/> first.
    /// </summary>
    /// <returns><see langword="true"/> once the job is updated.</returns>
    /// <exception cref="InvalidOperationException">No job has the request's id.</exception>
    Task<bool> UpdateJobSettingsAsync(UpdateCronJob request, CancellationToken cancellationToken);

    /// <summary>
    /// When any of the user's accounts last synced, in the server's local time, or <see langword="null"/> if none
    /// has synced yet.
    /// </summary>
    Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Queues a sync of all the user's connections, to run straight away in the background.
    ///<remarks>
    /// Returns once queued,
    /// <see cref="ISyncNotifications"/> says when it finishes.
    /// </remarks>
    /// </summary>
    Task RequestSyncAsync(CancellationToken cancellationToken);
}
