using Quartz;
using Quartz.Extensibility;

namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Tells admins open browsers as each background job starts and finishes, so the automation page can show it, see
/// <see cref="ISyncNotifier.JobChangedAsync"/>.
/// </summary>
public sealed class JobChangedListener : IJobListener
{
    private readonly ISyncNotifier _syncNotifier;

    /// <param name="syncNotifier">Tells browsers about syncs and jobs.</param>
    public JobChangedListener(ISyncNotifier syncNotifier) => _syncNotifier = syncNotifier;

    /// <inheritdoc/>
    public string Name => nameof(JobChangedListener);

    /// <inheritdoc/>
    public async ValueTask JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await _syncNotifier.JobChangedAsync(context.JobDetail.Key.Name);
    }

    /// <inheritdoc/>
    public ValueTask JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <inheritdoc/>
    public async ValueTask JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await _syncNotifier.JobChangedAsync(context.JobDetail.Key.Name);
    }

    /// <inheritdoc/>
    public ValueTask JobProgressChanged(IJobExecutionContext context, FireInstanceProgress progress,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
