namespace FinanceTracker.Contracts.Automation;

/// <summary>
/// A scheduled background job, shared by all users.
/// </summary>
/// <param name="Id">The job's id, for <see cref="UpdateCronJob.Id"/>.</param>
/// <param name="Function">The job TickerQ runs, e.g. <c>SyncAllOpenBankingDetailsAsync</c>.</param>
/// <param name="Description">What the job does.</param>
/// <param name="Expression">When the job runs, a 6 part cron expression starting with seconds.</param>
/// <param name="Retries">How many times a failed run is retried.</param>
/// <param name="RetryIntervals">Seconds to wait before each retry, or <see langword="null"/> for TickerQ's default.</param>
/// <param name="IsEnabled">Whether the job runs on its schedule.</param>
public record AutomationCron(
    Guid Id,
    string Function,
    string Description,
    string Expression,
    int Retries,
    int[]? RetryIntervals,
    bool IsEnabled);
