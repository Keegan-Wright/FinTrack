namespace FinanceTracker.Contracts.Automation;

/// <summary>
/// Changes to a scheduled job. Validated by <see cref="Validation.CronJobUpdateRequestValidator"/>.
/// </summary>
public class UpdateCronJob()
{
    /// <summary>
    /// The job to change, see <see cref="AutomationCron.Function"/>.
    /// </summary>
    public string Function { get; set; } = string.Empty;

    /// <summary>
    /// What the job does, shown on the automation page.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// When the job runs, a Quartz cron expression in UTC, see <see cref="AutomationCron.Expression"/>, e.g.
    /// <c>0 0 */6 * * ?</c>.
    /// </summary>
    public required string Expression { get; set; }

    /// <summary>
    /// How many times a failed run is retried.
    /// </summary>
    public int Retries { get; set; }

    /// <summary>
    /// Seconds to wait before each retry, one per retry.
    /// </summary>
    public int[] RetryIntervals { get; set; } = [];

    /// <summary>
    /// Whether the job runs on its schedule.
    /// </summary>
    public required bool IsEnabled { get; set; }
}
