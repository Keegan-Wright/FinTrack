using System.Runtime.CompilerServices;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Enums;
using FinanceTracker.Services.OpenBanking;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Base;

namespace FinanceTracker.BackgroundJobs;

public class BackgroundSyncAutomatedJobs
{
    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly IOpenBankingService _openBankingService;

    public BackgroundSyncAutomatedJobs(IOpenBankingService openBankingService,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory)
    {
        _openBankingService = openBankingService;
        _dbContextFactory = dbContextFactory;
    }

    [TickerFunction("SyncAllOpenBankingDetailsAsync", "0 0 */4 * * *")]
    public async Task SyncAllOpenBankingDetailsAsync(
        TickerFunctionContext context,
        CancellationToken cancellationToken)
    {
        context.CronOccurrenceOperations.SkipIfAlreadyRunning();

        await using FinanceTrackerContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        ConfiguredCancelableAsyncEnumerable<FinanceTrackerUser> userQuery = dbContext.Users
            .Include(x => x.Providers)
            .ThenInclude(x => x.Accounts)
            .ThenInclude(x => x.Transactions)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.AccountBalance)
            .Include(x => x.Providers).ThenInclude(x => x.Scopes)
            .Include(x => x.Providers).ThenInclude(x => x.Syncronisations)
            .AsSplitQuery()
            .AsAsyncEnumerable().WithCancellation(cancellationToken);

        await foreach (FinanceTrackerUser user in userQuery)
        {
            await _openBankingService.RunFunctionAsUser(user.Id, async () =>
            {
                foreach (OpenBankingProvider provider in user.Providers)
                {
                    await _openBankingService.BulkLoadProviderAsync(provider, SyncTypes.All, cancellationToken);
                }
            }, cancellationToken);
        }
    }
}
