using FinanceTracker.Contracts.Automation;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The scheduled background jobs and the signed in user's syncs.
/// </summary>
public interface IAutomationService
{
    /// <summary>
    /// The most runs <see cref="GetRunsAsync"/> returns at once.
    /// </summary>
    const int MaxRunsPageSize = 100;

    /// <summary>
    /// Every scheduled (cron) job. Jobs are deployment wide.
    /// </summary>
    IAsyncEnumerable<AutomationCron> GetJobsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// How each background job is doing, every scheduled job, then any job that has only run by hand.
    /// </summary>
    IAsyncEnumerable<AutomationJobSummary> GetJobSummariesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A page of a job's runs, on its schedule and by hand, newest first.
    /// </summary>
    /// <param name="function">The job, see <see cref="AutomationCron.Function"/>.</param>
    /// <param name="page">The page, from 0.</param>
    /// <param name="pageSize">Runs to a page, 1 to <see cref="MaxRunsPageSize"/>.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    Task<AutomationRunPage> GetRunsAsync(string function, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Queues a run of a scheduled job straight away, on top of its schedule. It shows in the job's runs as manual.
    /// </summary>
    /// <param name="id">The job, see <see cref="AutomationCron.Id"/>.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <exception cref="InvalidOperationException">No job has the id.</exception>
    /// <exception cref="JobAlreadyRunningException">A run of the job is going or queued.</exception>
    Task RunNowAsync(Guid id, CancellationToken cancellationToken);

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
