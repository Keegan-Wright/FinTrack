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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.NetWorth;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<INetWorthService>]
public class NetWorthService : ServiceBase<NetWorthService>, INetWorthService
{
    private readonly TimeProvider _timeProvider;

    public NetWorthService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<NetWorthService> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    public async Task<NetWorthHistory> GetNetWorthAsync(DateOnly? from, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        // Every day, as the changes this month and year need the days before the range too
        List<NetWorthSnapshot> history = await context.NetWorthSnapshots.OwnedBy(UserId).AsNoTracking()
            .OrderBy(static x => x.Date)
            .ToListAsync(cancellationToken);

        if (history.Count == 0)
        {
            return new NetWorthHistory();
        }

        NetWorthSnapshot latest = history[^1];
        DateOnly today = _timeProvider.Today;

        return new NetWorthHistory
        {
            Points = history.Where(x => from is null || x.Date >= from).Select(ToPoint).ToList(),
            ChangeThisMonth = ChangeSince(history, latest, new DateOnly(today.Year, today.Month, 1)),
            ChangeThisYear = ChangeSince(history, latest, new DateOnly(today.Year, 1, 1)),
            Breakdown = await BreakdownAsync(context, latest.Date, cancellationToken)
        };
    }

    public async IAsyncEnumerable<AccountBalanceHistory> GetBalanceHistoriesAsync(DateOnly? from,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<BalanceSnapshot> snapshots = context.BalanceSnapshots.OwnedBy(UserId).AsNoTracking();
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
    private static decimal? ChangeSince(List<NetWorthSnapshot> history, NetWorthSnapshot latest, DateOnly start)
    {
        NetWorthSnapshot? before = history.LastOrDefault(x => x.Date < start);
        if (before is null)
        {
            return null;
        }

        return latest.NetWorth - before.NetWorth;
    }

    /// <summary>
    /// Each account and card's balance on <paramref name="day"/>, and what each debt not linked to an account owed.
    /// </summary>
    private async Task<List<NetWorthItem>> BreakdownAsync(FinanceTrackerContext context, DateOnly day,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, BankAccount> accounts = await context.BankAccounts.OwnedBy(UserId).AsNoTracking()
            .ToDictionaryAsync(static x => x.Id, cancellationToken);
        List<BalanceSnapshot> balances = await context.BalanceSnapshots.OwnedBy(UserId).AsNoTracking()
            .Where(x => x.Date == day)
            .ToListAsync(cancellationToken);

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
