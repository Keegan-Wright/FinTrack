namespace FinanceTracker.Contracts.Automation;

/// <summary>
/// A scheduled background job, shared by all users.
/// </summary>
/// <param name="Function">The job, e.g. <c>SyncAllOpenBankingDetailsAsync</c>, which
/// <see cref="UpdateCronJob.Function"/> and run now name it by.</param>
/// <param name="Description">What the job does.</param>
/// <param name="Expression">When the job runs, a Quartz cron expression in UTC seconds first, then minutes, hours,
/// day of month, month and day of week, with <c>?</c> in one of the two day fields, e.g. <c>0 0 */4 * * ?</c>.</param>
/// <param name="Retries">How many times a failed run is retried.</param>
/// <param name="RetryIntervals">Seconds to wait before each retry, the last used for any after it.</param>
/// <param name="IsEnabled">Whether the job runs on its schedule.</param>
public record AutomationCron(
    string Function,
    string Description,
    string Expression,
    int Retries,
    int[] RetryIntervals,
    bool IsEnabled);
