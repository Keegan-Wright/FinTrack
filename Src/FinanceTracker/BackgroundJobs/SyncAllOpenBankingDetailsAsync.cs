using System.Diagnostics;
using System.Runtime.CompilerServices;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Enums;
using FinanceTracker.Services.OpenBanking;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace FinanceTracker.BackgroundJobs;


// ReSharper disable once ClassNeverInstantiated.Global
// Background job, ignore the warning
public class SyncAllOpenBankingDetailsAsync : ITickerFunction
{

    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly IOpenBankingService _openBankingService;

    public SyncAllOpenBankingDetailsAsync(IOpenBankingService openBankingService,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory)
    {
        _openBankingService = openBankingService;
        _dbContextFactory = dbContextFactory;
    }

    public async Task ExecuteAsync(TickerFunctionContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        context.CronOccurrenceOperations.SkipIfAlreadyRunning();

        using var activity = new ActivitySource("FinanceTracker");

        using var performingBackgroundSyncActivity = activity.StartActivity("PerformingBackgroundSync");

        await using FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        ConfiguredCancelableAsyncEnumerable<FinanceTrackerUser> userQuery = dbContext.Users
            .Include(x => x.Providers)!
            .ThenInclude(x => x.Accounts)!
            .ThenInclude(x => x.Transactions)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.AccountBalance)
            .Include(x => x.Providers)!.ThenInclude(x => x.Scopes)
            .Include(x => x.Providers)!.ThenInclude(x => x.Syncronisations)
            .AsSplitQuery()
            .AsAsyncEnumerable().WithCancellation(cancellationToken);


        await foreach (FinanceTrackerUser user in userQuery)
        {
            performingBackgroundSyncActivity!.AddEvent(new ActivityEvent($"Processing User {user.Id}"));
            await _openBankingService.RunFunctionAsUser(user.Id, async () =>
            {
                foreach (OpenBankingProvider provider in user.Providers!)
                {
                    try
                    {
                        performingBackgroundSyncActivity.AddEvent(
                            new ActivityEvent($"Processing Provider {provider.Id} for User {user.Id}"));

                        await _openBankingService.BulkLoadProviderAsync(provider, SyncTypes.All, cancellationToken);

                        performingBackgroundSyncActivity.AddEvent(
                            new ActivityEvent($"Finished Processing Provider {provider.Id} for User {user.Id}"));
                    }
                    catch (Exception ex)
                    {
                        performingBackgroundSyncActivity.AddException(ex);
                    }
                }

                performingBackgroundSyncActivity.AddEvent(
                    new ActivityEvent($"Finished Processing User {user.Id}"));
            });
        }

        performingBackgroundSyncActivity.AddEvent(new ActivityEvent("Background sync completed"));

    }
}
