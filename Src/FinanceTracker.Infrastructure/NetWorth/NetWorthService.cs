using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.NetWorth;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<INetWorthService>]
public class NetWorthService : ServiceBase<NetWorthService>, INetWorthService
{
    private readonly TimeProvider _timeProvider;
    private readonly IAccountScope _accountScope;

    public NetWorthService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<NetWorthService> logger,
        TimeProvider timeProvider, IAccountScope accountScope) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _accountScope = accountScope;
    }

    public async Task<NetWorthHistory> GetNetWorthAsync(DateOnly? from, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);

        // Every day, as the changes this month and year need the days before the range too
        List<NetWorthSnapshot> own = await context.NetWorthSnapshots.OwnedBy(UserId).AsNoTracking()
            .OrderBy(static x => x.Date)
            .ToListAsync(cancellationToken);
        List<NetWorthPoint> history = await WithSharedAccountsAsync(context, view, own, cancellationToken);

        if (history.Count == 0)
        {
            return new NetWorthHistory();
        }

        NetWorthPoint latest = history[^1];
        DateOnly today = _timeProvider.Today;

        return new NetWorthHistory
        {
            Points = history.Where(x => from is null || x.Date >= from).ToList(),
            ChangeThisMonth = ChangeSince(history, latest, new DateOnly(today.Year, today.Month, 1)),
            ChangeThisYear = ChangeSince(history, latest, new DateOnly(today.Year, 1, 1)),
            Breakdown = await BreakdownAsync(context, view, latest.Date, cancellationToken)
        };
    }

    /// <summary>
    /// The user's own daily net worth with the balances of the accounts shared with them added in, each shared account
    /// counted from its owner's daily snapshots. A day either side lacks carries the last known one forward, so the
    /// line doesn't dip where one history starts later than the other.
    /// </summary>
    private static async Task<List<NetWorthPoint>> WithSharedAccountsAsync(FinanceTrackerContext context,
        AccountsInView view, List<NetWorthSnapshot> own, CancellationToken cancellationToken)
    {
        List<NetWorthPoint> ownPoints = own.Select(ToPoint).ToList();
        if (view.SharedAccountIds.Count == 0)
        {
            return ownPoints;
        }

        List<Guid> sharedIds = view.SharedAccountIds.ToList();
        List<BalanceSnapshot> shared = await context.BalanceSnapshots.AsNoTracking()
            .Where(x => sharedIds.Contains(x.AccountId))
            .ToListAsync(cancellationToken);
        if (shared.Count == 0)
        {
            return ownPoints;
        }

        List<DateOnly> days = ownPoints.Select(static x => x.Date)
            .Union(shared.Select(static x => x.Date))
            .Order()
            .ToList();
        Dictionary<DateOnly, NetWorthPoint> ownByDay = ownPoints.ToDictionary(static x => x.Date);
        List<IGrouping<Guid, BalanceSnapshot>> byAccount = shared.GroupBy(static x => x.AccountId).ToList();

        List<NetWorthPoint> combined = [];
        NetWorthPoint? lastOwn = null;
        foreach (DateOnly day in days)
        {
            if (ownByDay.TryGetValue(day, out NetWorthPoint? ownToday))
            {
                lastOwn = ownToday;
            }

            decimal assets = lastOwn?.Assets ?? 0;
            decimal liabilities = lastOwn?.Liabilities ?? 0;
            bool isStale = lastOwn?.IsStale ?? false;
            bool isDerived = lastOwn?.IsDerived ?? false;

            foreach (IGrouping<Guid, BalanceSnapshot> account in byAccount)
            {
                BalanceSnapshot? latest = account.Where(x => x.Date <= day).MaxBy(static x => x.Date);
                if (latest is null)
                {
                    continue;
                }

                decimal current = latest.Balance.Current;
                if (current >= 0)
                {
                    assets += current;
                }
                else
                {
                    liabilities -= current;
                }

                isStale |= latest.IsStale;
                isDerived |= latest.Source == BalanceSnapshotSource.Derived;
            }

            combined.Add(new NetWorthPoint
            {
                Date = day,
                Assets = assets,
                Liabilities = liabilities,
                NetWorth = assets - liabilities,
                IsDerived = isDerived,
                IsStale = isStale
            });
        }

        return combined;
    }

    public async IAsyncEnumerable<AccountBalanceHistory> GetBalanceHistoriesAsync(DateOnly? from,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        IQueryable<BalanceSnapshot> snapshots = context.BalanceSnapshots.InView(view).AsNoTracking();
        if (from is { } first)
        {
            snapshots = snapshots.Where(x => x.Date >= first);
        }

        List<BalanceSnapshot> loaded = await snapshots.OrderBy(static x => x.Date).ToListAsync(cancellationToken);

        foreach (IGrouping<Guid, BalanceSnapshot> account in loaded.GroupBy(static x => x.AccountId))
        {
            yield return new AccountBalanceHistory
            {
                AccountId = account.Key,
                Points = account.Select(ToPoint).ToList()
            };
        }
    }

    internal static BalancePoint ToPoint(BalanceSnapshot snapshot) => new()
    {
        Date = snapshot.Date,
        Amount = snapshot.Balance.Current,
        IsDerived = snapshot.Source == BalanceSnapshotSource.Derived
    };

    private static NetWorthPoint ToPoint(NetWorthSnapshot snapshot) => new()
    {
        Date = snapshot.Date,
        Assets = snapshot.Assets,
        Liabilities = snapshot.Liabilities,
        NetWorth = snapshot.NetWorth,
        IsDerived = snapshot.IsDerived,
        IsStale = snapshot.IsStale
    };

    /// <summary>
    /// <paramref name="latest"/> less the last net worth before <paramref name="start"/>, null when there's none.
    /// </summary>
    private static decimal? ChangeSince(List<NetWorthPoint> history, NetWorthPoint latest, DateOnly start)
    {
        NetWorthPoint? before = history.LastOrDefault(x => x.Date < start);
        if (before is null)
        {
            return null;
        }

        return latest.NetWorth - before.NetWorth;
    }

    /// <summary>
    /// Each account and card's balance on <paramref name="day"/>, and what each debt not linked to an account owed.
    /// </summary>
    private async Task<List<NetWorthItem>> BreakdownAsync(FinanceTrackerContext context, AccountsInView view,
        DateOnly day, CancellationToken cancellationToken)
    {
        Dictionary<Guid, BankAccount> accounts = await context.BankAccounts.InView(view).AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, cancellationToken);
        List<BalanceSnapshot> balances = await context.BalanceSnapshots.OwnedBy(UserId).AsNoTracking()
            .Where(x => x.Date == day)
            .ToListAsync(cancellationToken);

        // A shared account counts as of its latest balance by then, as the line above does
        List<Guid> sharedIds = view.SharedAccountIds.ToList();
        List<BalanceSnapshot> shared = await context.BalanceSnapshots.AsNoTracking()
            .Where(x => sharedIds.Contains(x.AccountId) && x.Date <= day)
            .ToListAsync(cancellationToken);
        balances.AddRange(shared.GroupBy(static x => x.AccountId).Select(static x => x.MaxBy(s => s.Date)!));

        List<NetWorthItem> items = [];
        foreach (BalanceSnapshot balance in balances)
        {
            if (!accounts.TryGetValue(balance.AccountId, out BankAccount? account))
            {
                continue;
            }

            NetWorthItemKind kind = NetWorthItemKind.Account;
            if (account.Kind == BankAccountKind.Card)
            {
                kind = NetWorthItemKind.Card;
            }

            items.Add(new NetWorthItem { Name = account.DisplayName, Kind = kind, Amount = balance.Balance.Current });
        }

        items.AddRange(await DebtsAsync(context, day, cancellationToken));

        return items.OrderByDescending(static x => Math.Abs(x.Amount)).ToList();
    }

    /// <summary>
    /// What each debt not linked to an account owed on <paramref name="day"/>, those paid off left out. A linked one
    /// is in its account's balance already.
    /// </summary>
    private async Task<List<NetWorthItem>> DebtsAsync(FinanceTrackerContext context, DateOnly day,
        CancellationToken cancellationToken)
    {
        List<Debt> debts = await context.Debts.OwnedBy(UserId).AsNoTracking()
            .Where(static x => x.AccountId == null)
            .ToListAsync(cancellationToken);

        if (debts.Count == 0)
        {
            return [];
        }

        List<BankTransaction> payments = await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
            .Where(static x => x.DebtPayment != null && x.DebtPayment.DebtId != null)
            .ToListAsync(cancellationToken);

        return debts
            .Select(x => new NetWorthItem
            {
                Name = x.Name,
                Kind = NetWorthItemKind.Debt,
                Amount = -DebtCalculator.OwedOn(x, payments, _timeProvider.LocalTimeZone, day)
            })
            .Where(static x => x.Amount < 0)
            .ToList();
    }
}
