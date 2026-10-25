using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace FinanceTracker.Infrastructure.Automation;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IAutomationService>]
public class AutomationService :  ServiceBase<AutomationService>, IAutomationService
{

    private readonly ICronTickerManager<CronTickerEntity> _cronTickerManager;
    private readonly IOpenBankingSyncScheduler _syncScheduler;

    public AutomationService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<AutomationService> logger, ICronTickerManager<CronTickerEntity> cronTickerManager, IOpenBankingSyncScheduler syncScheduler) : base(currentUser,
        financeTrackerContextFactory, logger)
    {
        _cronTickerManager = cronTickerManager;
        _syncScheduler = syncScheduler;
    }

    public async IAsyncEnumerable<AutomationCron> GetJobsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (CronTickerEntity? cron in context.CronTickerEntities.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new AutomationCron(cron.Id,  cron.Function, cron.Description, cron.Expression, cron.Retries,
                cron.RetryIntervals, cron.IsEnabled);
        }
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

    public async Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime? lastSync = await context.SyncLog
            .Where(x => context.BankAccounts.Any(a => a.Id == x.AccountId && a.UserId == UserId))
            .MaxAsync(x => (DateTime?)x.OccurredAt, cancellationToken);

        return lastSync?.ToLocalTime();
    }

    // The user comes from the scope, never the page, so nobody can queue a sync of someone else's connections
    public Task RequestSyncAsync(CancellationToken cancellationToken) =>
        _syncScheduler.ScheduleUserSyncAsync(UserId, cancellationToken);
}
