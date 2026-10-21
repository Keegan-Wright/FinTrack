using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace FinanceTracker.Infrastructure.Automation;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IAutomationService>]
public class AutomationService :  ServiceBase<AutomationService>, IAutomationService
{

    private readonly ICronTickerManager<CronTickerEntity> _cronTickerManager;
    private readonly ITimeTickerManager<TimeTickerEntity> _timeTickerManager;

    public AutomationService(ClaimsPrincipal user,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<AutomationService> logger, ICronTickerManager<CronTickerEntity> cronTickerManager, ITimeTickerManager<TimeTickerEntity> timeTickerManager) : base(user,
        financeTrackerContextFactory, logger)
    {
        _cronTickerManager = cronTickerManager;
        _timeTickerManager = timeTickerManager;
    }

    public async IAsyncEnumerable<AutomationCron> GetJobsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var cron in context.CronTickerEntities.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new AutomationCron(cron.Id,  cron.Function, cron.Description, cron.Expression, cron.Retries,
                cron.RetryIntervals, cron.IsEnabled);
        }
    }

    public async Task<bool> UpdateJobSettingsAsync(UpdateCronJob request, CancellationToken cancellationToken)
    {
        await using var context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        var cron = await context.CronTickerEntities.FirstAsync(x => x.Id == request.Id, cancellationToken);

        cron.Description = request.Description;
        cron.RetryIntervals = request.RetryIntervals;
        cron.Retries = request.Retries;
        cron.Expression = request.Expression;
        cron.IsEnabled = request.IsEnabled;
        await _cronTickerManager.UpdateAsync(cron, cancellationToken);

        return true;
    }

    /// <summary>
    /// When any of the user's accounts last synced from the sync log
    /// </summary>
    public async Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken)
    {
        await using var context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        var lastSync = await context.SyncLog
            .Where(x => context.BankAccounts.Any(a => a.Id == x.AccountId && a.UserId == UserId))
            .MaxAsync(x => (DateTime?)x.OccurredAt, cancellationToken);

        return lastSync?.ToLocalTime();
    }
}
