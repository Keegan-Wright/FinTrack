using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Request.Automation;
using FinanceTracker.Models.Response.Automation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.DashboardDtos;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;
using TickerQ.Utilities.Interfaces.Managers;

namespace FinanceTracker.Services.Automation;

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

    public async IAsyncEnumerable<AutomationCronResponse> GetJobsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        await foreach (var cron in context.CronTickerEntities.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return new AutomationCronResponse(cron.Id,  cron.Function, cron.Description, cron.Expression, cron.Retries,
                cron.RetryIntervals, cron.IsEnabled);
        }
    }

    public async Task<bool> UpdateJobSettingsAsync(CronJobUpdateRequest request, CancellationToken cancellationToken)
    {
        await using var context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        var cron = await context.CronTickerEntities.FirstAsync(x => x.Id == request.Id, cancellationToken);

        cron.Description = request.Description;
        cron.RetryIntervals = cron.RetryIntervals;
        cron.Retries = cron.Retries;
        cron.Expression = request.Expression;
        cron.IsEnabled = request.IsEnabled;
        await _cronTickerManager.UpdateAsync(cron, cancellationToken);

        return true;
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(CancellationToken cancellationToken)
    {
        var eventName = "SyncAllOpenBankingDetailsAsync";
        await using var context = await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        var cron = await context.CronTickerEntities
            .FirstAsync(x => x.Function == eventName, cancellationToken);

        var latestCronOccurence = await context.CronTickerOccurrenceEntities
            .Where(x => x.CronTickerId == cron.Id)
            .Where(x => x.Status == TickerStatus.Done)
            .OrderByDescending(x => x.ExecutionTime)
            .FirstOrDefaultAsync(cancellationToken);

        var latestManaulTickerOccurence = await context.TimeTickerEntities
            .Where(x => x.Function == eventName)
            .Where(x => x.Status == TickerStatus.Done)
            .OrderByDescending(x => x.ExecutionTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestManaulTickerOccurence?.ExecutionTime > latestCronOccurence?.ExecutionTime)
            return latestManaulTickerOccurence.ExecutionTime.Value.ToLocalTime();
        else
            return latestCronOccurence?.ExecutionTime.ToLocalTime();

    }
}
