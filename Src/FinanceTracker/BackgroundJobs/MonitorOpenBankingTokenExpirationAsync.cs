// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using FinanceTracker.Contracts;
using FinanceTracker.Domain;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace FinanceTracker.BackgroundJobs;


// ReSharper disable once ClassNeverInstantiated.Global
// Background job, ignore the warning
public class MonitorOpenBankingTokenExpirationAsync : ITickerFunction
{

    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly IOpenBankingService _openBankingService;
    private readonly IBackgroundSyncService _syncService;

    public MonitorOpenBankingTokenExpirationAsync(IOpenBankingService openBankingService,
        IDbContextFactory<FinanceTrackerContext> dbContextFactory, IBackgroundSyncService syncService)
    {
        _openBankingService = openBankingService;
        _dbContextFactory = dbContextFactory;
        _syncService = syncService;
    }

    public async Task ExecuteAsync(TickerFunctionContext context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        context.CronOccurrenceOperations.SkipIfAlreadyRunning();

        using var activity = new ActivitySource("FinanceTracker");

        using var performingMonitoringActivity = activity.StartActivity();

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
            performingMonitoringActivity!.AddEvent(new ActivityEvent($"Processing User {user.Id}"));
            await _openBankingService.RunFunctionAsUser(user.Id, async () =>
            {
                foreach (var accessToken in user.OpenBankingAccessTokens!.Where(x => !x.Expired))
                {
                    if (accessToken.Created.AddDays(90) > DateTime.UtcNow)
                    {
                        accessToken.Expire();
                    }
                    //TODO Notification
                }


                performingMonitoringActivity.AddEvent(
                    new ActivityEvent($"Finished Processing User {user.Id}"));
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        performingMonitoringActivity?.AddEvent(new ActivityEvent("Monitor OpenBanking Token Expiration completed"));

        await _syncService.NotifySyncComplete();
    }
}
