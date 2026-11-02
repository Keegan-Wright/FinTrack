using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking.Balances;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IBalanceSnapshotService>]
public class BalanceSnapshotService : ServiceBase<BalanceSnapshotService>, IBalanceSnapshotService
{
    private readonly TimeProvider _timeProvider;

    public BalanceSnapshotService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<BalanceSnapshotService> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    public async Task<NetWorthSnapshot?> SnapshotAsync(DateOnly day, DateTime balancesSyncedFrom,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Balances are encrypted, so accounts without one are left out in memory
        List<BankAccount> accounts = await context.BankAccounts.OwnedBy(UserId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        List<BankAccount> withBalance = accounts.Where(static x => x.Balance is not null).ToList();

        if (withBalance.Count == 0)
        {
            Logger.LogNoBalancesToSnapshot(UserId, day);
            return null;
        }

        List<Guid> accountIds = withBalance.Select(static x => x.Id).ToList();
        HashSet<Guid> refreshed = (await context.SyncLog
                .Where(x => accountIds.Contains(x.AccountId) && x.Type == SyncType.Balance &&
                            x.OccurredAt >= balancesSyncedFrom)
                .Select(static x => x.AccountId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        Dictionary<Guid, BalanceSnapshot> snapshots = await context.BalanceSnapshots.OwnedBy(UserId)
            .Where(x => x.Date == day)
            .ToDictionaryAsync(static x => x.AccountId, cancellationToken);

        foreach (BankAccount account in withBalance)
        {
            bool synced = refreshed.Contains(account.Id);
            if (snapshots.TryGetValue(account.Id, out BalanceSnapshot? snapshot))
            {
                snapshot.Refresh(account, synced);
                continue;
            }

            snapshot = account.SnapshotBalance(day, synced);
            context.BalanceSnapshots.Add(snapshot);
            snapshots.Add(account.Id, snapshot);
        }

        List<BalanceSnapshot> balances = snapshots.Values.ToList();
        decimal debts = await DebtsOwedAsync(context, day, cancellationToken);

        NetWorthSnapshot? netWorth = await context.NetWorthSnapshots.OwnedBy(UserId)
            .SingleOrDefaultAsync(x => x.Date == day, cancellationToken);
        if (netWorth is null)
        {
            netWorth = new NetWorthSnapshot(UserId, day, balances, debts);
            context.NetWorthSnapshots.Add(netWorth);
        }
        else
        {
            netWorth.Total(balances, debts);
        }

        await context.SaveChangesAsync(cancellationToken);

        Logger.LogSnapshotBalances(UserId, day, refreshed.Count, balances.Count(static x => x.IsStale));

        return netWorth;
    }

    /// <summary>
    /// What the debts not linked to an account owed on <paramref name="day"/>. Linked ones are in their account's
    /// balance already.
    /// </summary>
    private async Task<decimal> DebtsOwedAsync(FinanceTrackerContext context, DateOnly day,
        CancellationToken cancellationToken)
    {
        List<Debt> debts = await context.Debts.OwnedBy(UserId).AsNoTracking()
            .Where(static x => x.AccountId == null)
            .ToListAsync(cancellationToken);

        if (debts.Count == 0)
        {
            return 0;
        }

        List<BankTransaction> payments = await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
            .Where(static x => x.DebtPayment != null && x.DebtPayment.DebtId != null)
            .ToListAsync(cancellationToken);

        return debts.Sum(x => DebtCalculator.OwedOn(x, payments, _timeProvider.LocalTimeZone, day));
    }
}
