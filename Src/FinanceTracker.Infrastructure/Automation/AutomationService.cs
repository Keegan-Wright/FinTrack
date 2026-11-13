using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz.Extensibility;
using Quartz;

namespace FinanceTracker.Infrastructure.Automation;

/// <summary>
/// The background jobs as Quartz keeps them each job's schedule is its cron trigger, see <see cref="JobSchedules"/>,
/// and its runs are Quartz's execution history.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IAutomationService>]
public class AutomationService : ServiceBase<AutomationService>, IAutomationService
{
    // Enough of the newest runs to find the success rate's finished ones among retries
    private const int SuccessRateLookBack = AutomationJobSummary.SuccessRateRuns * 3;

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IExecutionHistoryStore _history;
    private readonly IOpenBankingSyncScheduler _syncScheduler;
    private readonly TimeProvider _timeProvider;

    public AutomationService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<AutomationService> logger,
        ISchedulerFactory schedulerFactory, IExecutionHistoryStore history, IOpenBankingSyncScheduler syncScheduler,
        TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _schedulerFactory = schedulerFactory;
        _history = history;
        _syncScheduler = syncScheduler;
        _timeProvider = timeProvider;
    }

    public async IAsyncEnumerable<AutomationCron> GetJobsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        foreach (ICronTrigger schedule in await SchedulesAsync(scheduler, cancellationToken))
        {
            yield return await ToCronAsync(scheduler, schedule, cancellationToken);
        }
    }

    public async IAsyncEnumerable<AutomationJobSummary> GetJobSummariesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        List<ICronTrigger> schedules = await SchedulesAsync(scheduler, cancellationToken);

        foreach (ICronTrigger schedule in schedules)
        {
            AutomationCron cron = await ToCronAsync(scheduler, schedule, cancellationToken);
            yield return await SummariseAsync(scheduler, schedule.JobKey, cron, schedule, cancellationToken);
        }

        // Jobs with no schedule, like a user's sync now, whether or not they've run yet
        HashSet<JobKey> scheduled = schedules.Select(static x => x.JobKey).ToHashSet();
        List<JobKey> jobs = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);
        IEnumerable<JobKey> unscheduled = jobs.Where(x => !scheduled.Contains(x))
            .OrderBy(static x => x.Name, StringComparer.Ordinal);
        foreach (JobKey job in unscheduled)
        {
            yield return await SummariseAsync(scheduler, job, cron: null, schedule: null, cancellationToken);
        }
    }

    public async Task<AutomationRunPage> GetRunsAsync(string function, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, IAutomationService.MaxRunsPageSize);

        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        JobKey job = new(function);
        PagedResult<ExecutionHistoryEntry> history = await _history.QueryExecutions(new ExecutionHistoryQuery
        {
            SchedulerName = scheduler.SchedulerName,
            Job = job,
            Skip = page * pageSize,
            Take = pageSize,
            IncludeTotalCount = true
        }, cancellationToken);

        List<AutomationRun> runs = history.Items.Select(ToRun).ToList();
        int total = history.TotalCount ?? runs.Count;

        // A run that's going isn't in the history until it finishes
        if (await RunningAsync(scheduler, job, cancellationToken) is { } running)
        {
            total++;
            if (page == 0)
            {
                runs.Insert(0, running);
            }
        }

        return new AutomationRunPage(runs, total);
    }

    public async Task<bool> UpdateJobSettingsAsync(UpdateCronJob request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!JobSchedules.IsReadable(request.Expression))
        {
            throw new ArgumentException($"Quartz can't read the cron expression {request.Expression}",
                nameof(request));
        }

        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        TriggerKey key = JobSchedules.KeyFor(request.Function);
        if (await scheduler.GetTrigger(key, cancellationToken) is null)
        {
            throw new NotFoundException($"{request.Function} has no schedule");
        }

        ITrigger schedule = JobSchedules.Build(request.Function, request.Description, request.Expression,
            request.Retries, request.RetryIntervals);
        await scheduler.RescheduleJob(key, schedule, cancellationToken);

        if (request.IsEnabled)
        {
            await scheduler.ResumeTrigger(key, cancellationToken);
        }
        else
        {
            await scheduler.PauseTrigger(key, cancellationToken);
        }

        Logger.LogJobSettingsChanged(UserId, request.Function, request.IsEnabled, request.Expression);
        return true;
    }

    public async Task RunNowAsync(string function, CancellationToken cancellationToken)
    {
        IScheduler scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        if (await scheduler.GetTrigger(JobSchedules.KeyFor(function), cancellationToken) is null)
        {
            throw new NotFoundException($"{function} has no schedule");
        }

        JobKey job = new(function);
        if (await RunningAsync(scheduler, job, cancellationToken) is not null)
        {
            throw new JobAlreadyRunningException(function);
        }

        await scheduler.TriggerJob(job, null, cancellationToken);
        Logger.LogJobRunNow(UserId, function);
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime? lastSync = await context.SyncLog
            .Where(x => context.BankAccounts.Any(a => a.Id == x.AccountId && a.UserId == UserId))
            .MaxAsync(x => (DateTime?)x.OccurredAt, cancellationToken);

        return lastSync?.ToLocalTime();
    }

    private async Task<AutomationJobSummary> SummariseAsync(IScheduler scheduler, JobKey job, AutomationCron? cron,
        ICronTrigger? schedule, CancellationToken cancellationToken)
    {
        PagedResult<ExecutionHistoryEntry> latest = await _history.QueryExecutions(new ExecutionHistoryQuery
        {
            SchedulerName = scheduler.SchedulerName,
            Job = job,
            Take = SuccessRateLookBack
        }, cancellationToken);

        // A failure that's being retried isn't finished, its retry says how it went
        List<ExecutionHistoryEntry> finished = latest.Items
            .Where(static x => !x.RetryScheduled &&
                               x.EffectiveResult is JobRunResult.Succeeded or JobRunResult.Failed)
            .Take(AutomationJobSummary.SuccessRateRuns)
            .ToList();

        int? successRate = null;
        if (finished.Count > 0)
        {
            int succeeded = finished.Count(static x => x.Succeeded);
            successRate = (int)Math.Round(100d * succeeded / finished.Count);
        }

        AutomationRun? lastRun = await RunningAsync(scheduler, job, cancellationToken);
        if (lastRun is null && latest.Items.Count > 0)
        {
            lastRun = ToRun(latest.Items[0]);
        }

        return new AutomationJobSummary(job.Name, cron, NextRunAt(cron, schedule), lastRun, successRate,
            finished.Count);
    }

    // A paused job won't run on its schedule
    private DateTime? NextRunAt(AutomationCron? cron, ICronTrigger? schedule)
    {
        if (cron is not { IsEnabled: true } || schedule?.NextFireTimeUtc is not { } next)
        {
            return null;
        }

        return _timeProvider.ToLocal(next.UtcDateTime);
    }

    /// <summary>
    /// The run of <paramref name="job"/> going now, from its schedule or by hand, null when none is.
    /// </summary>
    private async Task<AutomationRun?> RunningAsync(IScheduler scheduler, JobKey job,
        CancellationToken cancellationToken)
    {
        foreach (ITrigger trigger in await scheduler.GetTriggersOfJob(job, cancellationToken))
        {
            if (await scheduler.GetTriggerState(trigger.Key, cancellationToken) != TriggerState.Executing)
            {
                continue;
            }

            DateTime startedAt = (trigger.PreviousFireTimeUtc ?? _timeProvider.GetUtcNow()).UtcDateTime;
            AutomationRunStatus status = AutomationRunStatus.Running;
            if (trigger.RetryAttempt > 0)
            {
                status = AutomationRunStatus.Retrying;
            }

            bool manual = trigger.Key.Group != JobNames.ScheduleGroup;
            return new AutomationRun(trigger.Key.ToString(), job.Name, status, manual,
                _timeProvider.ToLocal(startedAt), _timeProvider.ToLocal(startedAt), null, trigger.RetryAttempt,
                null);
        }

        return null;
    }

    private AutomationRun ToRun(ExecutionHistoryEntry entry)
    {
        DateTime firedAt = _timeProvider.ToLocal(entry.FiredAtUtc.UtcDateTime);
        return new AutomationRun(entry.EntryId ?? entry.FireInstanceId ?? firedAt.ToString("O"), entry.JobName,
            StatusOf(entry), entry.Manual, firedAt, firedAt, entry.Duration, entry.RetryAttempt,
            entry.ExceptionMessage);
    }

    private static AutomationRunStatus StatusOf(ExecutionHistoryEntry entry) => entry.EffectiveResult switch
    {
        JobRunResult.Succeeded => AutomationRunStatus.Done,
        JobRunResult.Skipped => AutomationRunStatus.Skipped,
        JobRunResult.Cancelled => AutomationRunStatus.Cancelled,
        _ when entry.RetryScheduled => AutomationRunStatus.Retrying,
        _ => AutomationRunStatus.Failed
    };

    private static async Task<List<ICronTrigger>> SchedulesAsync(IScheduler scheduler,
        CancellationToken cancellationToken)
    {
        List<TriggerKey> keys = await scheduler.GetTriggerKeys(
            GroupMatcher<TriggerKey>.GroupEquals(JobNames.ScheduleGroup), cancellationToken);

        List<ICronTrigger> schedules = [];
        foreach (TriggerKey key in keys.OrderBy(static x => x.Name, StringComparer.Ordinal))
        {
            if (await scheduler.GetTrigger(key, cancellationToken) is ICronTrigger schedule)
            {
                schedules.Add(schedule);
            }
        }

        return schedules;
    }

    private static async Task<AutomationCron> ToCronAsync(IScheduler scheduler, ICronTrigger schedule,
        CancellationToken cancellationToken)
    {
        TriggerState state = await scheduler.GetTriggerState(schedule.Key, cancellationToken);
        int[] retryIntervals = JobSchedules.RetryIntervals(schedule.RetryPolicy);

        return new AutomationCron(schedule.JobKey.Name, schedule.Description ?? schedule.JobKey.Name,
            schedule.CronExpressionString ?? string.Empty, retryIntervals.Length, retryIntervals,
            state != TriggerState.Paused);
    }

    // The user comes from the scope, never the page, so nobody can queue a sync of someone else's connections
    public Task RequestSyncAsync(CancellationToken cancellationToken) =>
        _syncScheduler.ScheduleUserSyncAsync(UserId, cancellationToken);
}
