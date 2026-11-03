using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models;

namespace FinanceTracker.Infrastructure.Automation;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IAutomationService>]
public class AutomationService :  ServiceBase<AutomationService>, IAutomationService
{
    // Runs that count towards a job's success rate, and runs that have started
    private static readonly TickerStatus[] Finished = [TickerStatus.Done, TickerStatus.DueDone, TickerStatus.Failed];

    private static readonly TickerStatus[] Started =
    [
        TickerStatus.InProgress, TickerStatus.Done, TickerStatus.DueDone, TickerStatus.Failed, TickerStatus.Cancelled,
        TickerStatus.Skipped
    ];

    // A run started by hand is busy from being asked for until it finishes
    private static readonly TickerStatus[] ManualBusy = [TickerStatus.Idle, TickerStatus.Queued, TickerStatus.InProgress];

    private readonly ICronTickerManager<CronTickerEntity> _cronTickerManager;
    private readonly ITimeTickerManager<TimeTickerEntity> _timeTickerManager;
    private readonly IOpenBankingSyncScheduler _syncScheduler;
    private readonly TimeProvider _timeProvider;

    public AutomationService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<AutomationService> logger,
        ICronTickerManager<CronTickerEntity> cronTickerManager, ITimeTickerManager<TimeTickerEntity> timeTickerManager,
        IOpenBankingSyncScheduler syncScheduler, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _cronTickerManager = cronTickerManager;
        _timeTickerManager = timeTickerManager;
        _syncScheduler = syncScheduler;
        _timeProvider = timeProvider;
    }

    public async IAsyncEnumerable<AutomationCron> GetJobsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (CronTickerEntity? cron in context.CronTickerEntities.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return ToCron(cron);
        }
    }

    public async IAsyncEnumerable<AutomationJobSummary> GetJobSummariesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<CronTickerEntity> crons = await context.CronTickerEntities.AsNoTracking()
            .OrderBy(x => x.Function)
            .ToListAsync(cancellationToken);

        foreach (CronTickerEntity cron in crons)
        {
            yield return await SummariseAsync(context, cron.Function, cron, cancellationToken);
        }

        // Jobs with no schedule, like a user's sync now
        List<string> manualOnly = await context.TimeTickerEntities.AsNoTracking()
            .Where(x => !context.CronTickerEntities.Any(cron => cron.Function == x.Function))
            .Select(x => x.Function)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        foreach (string function in manualOnly)
        {
            yield return await SummariseAsync(context, function, cron: null, cancellationToken);
        }
    }

    public async Task<AutomationRunPage> GetRunsAsync(string function, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, IAutomationService.MaxRunsPageSize);

        await using FinanceTrackerContext context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        Guid? cronId = await context.CronTickerEntities.AsNoTracking()
            .Where(x => x.Function == function)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        List<AutomationRun> runs = await LatestRunsAsync(context, function, cronId, statuses: null, page * pageSize,
            pageSize, cancellationToken);

        int total = await context.TimeTickerEntities.CountAsync(x => x.Function == function, cancellationToken);
        if (cronId is not null)
        {
            total += await context.CronTickerOccurrenceEntities.CountAsync(x => x.CronTickerId == cronId,
                cancellationToken);
        }

        return new AutomationRunPage(runs, total);
    }

    public async Task<bool> UpdateJobSettingsAsync(UpdateCronJob request, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        CronTickerEntity cron = await context.CronTickerEntities.FirstAsync(x => x.Id == request.Id, cancellationToken);

        cron.Description = request.Description;
        cron.RetryIntervals = request.RetryIntervals;
        cron.Retries = request.Retries;
        cron.Expression = request.Expression;
        cron.IsEnabled = request.IsEnabled;
        await _cronTickerManager.UpdateAsync(cron, cancellationToken);

        return true;
    }

    public async Task RunNowAsync(Guid id, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        CronTickerEntity cron = await context.CronTickerEntities.AsNoTracking()
            .FirstAsync(x => x.Id == id, cancellationToken);

        // TickerQ lines up and queues the next scheduled run well before it's due, so a queued one only counts
        // once it's due
        DateTime now = _timeProvider.UtcNow;
        bool scheduledRunBusy = await context.CronTickerOccurrenceEntities
            .AnyAsync(x => x.CronTickerId == id &&
                           (x.Status == TickerStatus.InProgress ||
                            (x.Status == TickerStatus.Queued && x.ExecutionTime <= now)), cancellationToken);
        bool manualRunBusy = await context.TimeTickerEntities
            .AnyAsync(x => x.Function == cron.Function && ManualBusy.Contains(x.Status), cancellationToken);
        if (scheduledRunBusy || manualRunBusy)
        {
            throw new JobAlreadyRunningException(cron.Function);
        }

        // Retried like a scheduled run of the job
        TickerResult<TimeTickerEntity> queued = await _timeTickerManager.AddAsync(new TimeTickerEntity
        {
            Id = Guid.NewGuid(),
            Function = cron.Function,
            Description = "Run now",
            ExecutionTime = now,
            Retries = cron.Retries,
            RetryIntervals = cron.RetryIntervals
        }, cancellationToken);

        if (queued is { IsSucceeded: false, Exception: { } failure })
        {
            ExceptionDispatchInfo.Throw(failure);
        }
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime? lastSync = await context.SyncLog
            .Where(x => context.BankAccounts.Any(a => a.Id == x.AccountId && a.UserId == UserId))
            .MaxAsync(x => (DateTime?)x.OccurredAt, cancellationToken);

        return lastSync?.ToLocalTime();
    }

    private async Task<AutomationJobSummary> SummariseAsync(FinanceTrackerContext context, string function,
        CronTickerEntity? cron, CancellationToken cancellationToken)
    {
        List<AutomationRun> lastStarted = await LatestRunsAsync(context, function, cron?.Id, Started, skip: 0, take: 1,
            cancellationToken);
        List<AutomationRun> finished = await LatestRunsAsync(context, function, cron?.Id, Finished, skip: 0,
            AutomationJobSummary.SuccessRateRuns, cancellationToken);

        int? successRate = null;
        if (finished.Count > 0)
        {
            int succeeded = finished.Count(x => x.Status == AutomationRunStatus.Done);
            successRate = (int)Math.Round(100d * succeeded / finished.Count);
        }

        AutomationCron? schedule = cron is null ? null : ToCron(cron);
        return new AutomationJobSummary(function, schedule, NextRunAt(cron), lastStarted.FirstOrDefault(),
            successRate, finished.Count);
    }

    // A paused job, or one TickerQ has paused itself, won't run on its schedule
    private DateTime? NextRunAt(CronTickerEntity? cron)
    {
        if (cron is null || !cron.IsEnabled || cron.IsSystemPaused)
        {
            return null;
        }

        DateTime? next = CronSchedule.NextAfter(cron.Expression, _timeProvider.UtcNow);
        if (next is null)
        {
            return null;
        }

        return _timeProvider.ToLocal(next.Value);
    }

    /// <summary>
    /// The newest runs of <paramref name="function"/>, from its schedule's occurrences and the times it was run by
    /// hand, merged by when each was due.
    /// </summary>
    /// <param name="statuses">Only runs in one of these, or <see langword="null"/> for any.</param>
    private async Task<List<AutomationRun>> LatestRunsAsync(FinanceTrackerContext context, string function,
        Guid? cronId, TickerStatus[]? statuses, int skip, int take, CancellationToken cancellationToken)
    {
        // Either source could hold the whole page, so each gives up to everything before and on it
        int upTo = skip + take;
        List<AutomationRun> runs = [];

        if (cronId is not null)
        {
            IQueryable<CronTickerOccurrenceEntity<CronTickerEntity>> occurrences = context.CronTickerOccurrenceEntities
                .AsNoTracking()
                .Where(x => x.CronTickerId == cronId);
            if (statuses is not null)
            {
                occurrences = occurrences.Where(x => statuses.Contains(x.Status));
            }

            List<CronTickerOccurrenceEntity<CronTickerEntity>> scheduled = await occurrences
                .OrderByDescending(x => x.ExecutionTime)
                .Take(upTo)
                .ToListAsync(cancellationToken);
            runs.AddRange(scheduled.Select(x => x.ToRun(function, _timeProvider)));
        }

        IQueryable<TimeTickerEntity> tickers = context.TimeTickerEntities.AsNoTracking()
            .Where(x => x.Function == function);
        if (statuses is not null)
        {
            tickers = tickers.Where(x => statuses.Contains(x.Status));
        }

        List<TimeTickerEntity> manual = await tickers
            .OrderByDescending(x => x.ExecutionTime ?? x.CreatedAt)
            .Take(upTo)
            .ToListAsync(cancellationToken);
        runs.AddRange(manual.Select(x => x.ToRun(_timeProvider)));

        return runs.OrderByDescending(x => x.DueAt)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    private static AutomationCron ToCron(CronTickerEntity cron) =>
        new(cron.Id, cron.Function, cron.Description, cron.Expression, cron.Retries, cron.RetryIntervals,
            cron.IsEnabled);

    // The user comes from the scope, never the page, so nobody can queue a sync of someone else's connections
    public Task RequestSyncAsync(CancellationToken cancellationToken) =>
        _syncScheduler.ScheduleUserSyncAsync(UserId, cancellationToken);
}
