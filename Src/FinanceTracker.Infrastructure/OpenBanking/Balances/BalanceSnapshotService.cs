using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Telemetry;
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
            InfrastructureTelemetry.TagCurrent((TelemetryTags.SnapshotsRefreshed, 0), (TelemetryTags.SnapshotsStale, 0));
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

        int stale = balances.Count(static x => x.IsStale);
        Logger.LogSnapshotBalances(UserId, day, refreshed.Count, stale);
        InfrastructureTelemetry.TagCurrent(
            (TelemetryTags.SnapshotsRefreshed, refreshed.Count),
            (TelemetryTags.SnapshotsStale, stale));

        return netWorth;
    }

    public async Task<int> BackfillAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Balances are encrypted, so accounts without one are left out in memory
        List<BankAccount> accounts = (await context.BankAccounts.OwnedBy(UserId)
                .Where(static x => !x.HistoryBackfilled)
                .ToListAsync(cancellationToken))
            .Where(static x => x.Balance is not null)
            .ToList();

        if (accounts.Count == 0)
        {
            InfrastructureTelemetry.TagCurrent((TelemetryTags.Accounts, 0), (TelemetryTags.Days, 0));
            return 0;
        }

        List<Guid> accountIds = accounts.Select(static x => x.Id).ToList();

        ILookup<Guid, BankTransaction> transactions = (await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
                .Where(x => accountIds.Contains(x.AccountId))
                .ToListAsync(cancellationToken))
            .ToLookup(static x => x.AccountId);

        ILookup<Guid, BalanceSnapshot> snapshots = (await context.BalanceSnapshots.OwnedBy(UserId).AsNoTracking()
                .Where(x => accountIds.Contains(x.AccountId))
                .ToListAsync(cancellationToken))
            .ToLookup(static x => x.AccountId);

        HashSet<DateOnly> days = [];
        foreach (BankAccount account in accounts)
        {
            IReadOnlyList<BalanceSnapshot> derived = BalanceHistory.Backfill(account,
                snapshots[account.Id].ToList(), transactions[account.Id], _timeProvider.LocalTimeZone);

            context.BalanceSnapshots.AddRange(derived);
            days.UnionWith(derived.Select(static x => x.Date));
            account.MarkHistoryBackfilled();
        }

        await context.SaveChangesAsync(cancellationToken);
        await RetotalAsync(context, days, cancellationToken);

        Logger.LogBackfilledBalanceHistory(UserId, accounts.Count, days.Count);
        InfrastructureTelemetry.TagCurrent((TelemetryTags.Accounts, accounts.Count), (TelemetryTags.Days, days.Count));
        return days.Count;
    }

    /// <summary>
    /// Totals each of <paramref name="days"/>' net worth again from all its balances, adding the days that had none.
    /// </summary>
    private async Task RetotalAsync(FinanceTrackerContext context, HashSet<DateOnly> days,
        CancellationToken cancellationToken)
    {
        if (days.Count == 0)
        {
            return;
        }

        ILookup<DateOnly, BalanceSnapshot> balances = (await context.BalanceSnapshots.OwnedBy(UserId).AsNoTracking()
                .Where(x => days.Contains(x.Date))
                .ToListAsync(cancellationToken))
            .ToLookup(static x => x.Date);

        Dictionary<DateOnly, NetWorthSnapshot> netWorths = await context.NetWorthSnapshots.OwnedBy(UserId)
            .Where(x => days.Contains(x.Date))
            .ToDictionaryAsync(static x => x.Date, cancellationToken);

        DebtsOwed debts = await LoadDebtsAsync(context, cancellationToken);

        foreach (DateOnly day in days)
        {
            List<BalanceSnapshot> dayBalances = balances[day].ToList();
            decimal owed = debts.On(day);

            if (netWorths.TryGetValue(day, out NetWorthSnapshot? netWorth))
            {
                netWorth.Total(dayBalances, owed);
                continue;
            }

            context.NetWorthSnapshots.Add(new NetWorthSnapshot(UserId, day, dayBalances, owed));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// What the debts not linked to an account owed on <paramref name="day"/>. Linked ones are in their account's
    /// balance already.
    /// </summary>
    private async Task<decimal> DebtsOwedAsync(FinanceTrackerContext context, DateOnly day,
        CancellationToken cancellationToken) =>
        (await LoadDebtsAsync(context, cancellationToken)).On(day);

    private async Task<DebtsOwed> LoadDebtsAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        List<Debt> debts = await context.Debts.OwnedBy(UserId).AsNoTracking()
            .Where(static x => x.AccountId == null)
            .ToListAsync(cancellationToken);

        if (debts.Count == 0)
        {
            return new DebtsOwed([], [], _timeProvider.LocalTimeZone);
        }

        List<BankTransaction> payments = await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
            .Where(static x => x.DebtPayment != null && x.DebtPayment.DebtId != null)
            .ToListAsync(cancellationToken);

        return new DebtsOwed(debts, payments, _timeProvider.LocalTimeZone);
    }
}
