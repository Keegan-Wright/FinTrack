using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.OpenBanking.Balances;

/// <summary>
/// Works out an account's past closing balances from the balances it synced and its transactions, for the days it has
/// no snapshot. before it was connected, and gaps with no sync.
/// </summary>
/// <remarks>
/// A day's closing balance is the next day's less what happened on it, so each synced balance is worked back one
/// day at a time. Only settled transactions count, as the bank's current balance leaves pending ones out. A card's
/// balance is what's owed, negated, and its purchases are negative, so the same sum works for cards.
/// </remarks>
public static class BalanceHistory
{
    /// <summary>
    /// <see cref="BalanceSnapshotSource.Derived"/> snapshots for the days before <paramref name="account"/>'s latest
    /// synced balance that have none, back to the day before its first transaction. Days that already have a snapshot
    /// are left as they are.
    /// </summary>
    /// <param name="snapshots">The account's snapshots. Those synced and not stale are worked back from, a stale one
    /// holds a balance from some earlier day so it's passed over.</param>
    /// <param name="transactions">The account's transactions.</param>
    /// <param name="timeZone">Where the user is, so a transaction falls on the day it was where they are.</param>
    /// <exception cref="ArgumentException">A snapshot or transaction isn't the account's.</exception>
    public static IReadOnlyList<BalanceSnapshot> Backfill(BankAccount account,
        IReadOnlyCollection<BalanceSnapshot> snapshots, IEnumerable<BankTransaction> transactions,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(timeZone);

        if (snapshots.Any(x => x.AccountId != account.Id))
        {
            throw new ArgumentException($"A snapshot isn't for account {account.Id}.", nameof(snapshots));
        }

        Dictionary<DateOnly, decimal> movedOn = MovedOn(account, transactions, timeZone);
        Dictionary<DateOnly, BalanceSnapshot> byDay = snapshots.ToDictionary(static x => x.Date);
        BalanceSnapshot? latest = snapshots
            .Where(IsWorkedBackFrom)
            .MaxBy(static x => x.Date);

        if (latest is null || movedOn.Count == 0)
        {
            return [];
        }

        DateOnly earliest = movedOn.Keys.Min().AddDays(-1);
        AccountBalance closing = latest.Balance;
        List<BalanceSnapshot> derived = [];

        for (DateOnly day = latest.Date.AddDays(-1); day >= earliest; day = day.AddDays(-1))
        {
            // The day after's closing balance less what moved on it
            closing = Before(closing, movedOn.GetValueOrDefault(day.AddDays(1)), day);

            if (!byDay.TryGetValue(day, out BalanceSnapshot? existing))
            {
                derived.Add(new BalanceSnapshot(account.UserId, account.Id, day, closing,
                    BalanceSnapshotSource.Derived, isStale: false));
                continue;
            }

            // A real balance is worked back from instead, so the history stays closest to what the bank said
            if (IsWorkedBackFrom(existing))
            {
                closing = existing.Balance;
            }
        }

        return derived;
    }

    private static bool IsWorkedBackFrom(BalanceSnapshot snapshot) =>
        snapshot is { Source: BalanceSnapshotSource.Synced, IsStale: false };

    /// <summary>
    /// What settled on each day where the user is, money in positive and out negative.
    /// </summary>
    private static Dictionary<DateOnly, decimal> MovedOn(BankAccount account, IEnumerable<BankTransaction> transactions,
        TimeZoneInfo timeZone)
    {
        Dictionary<DateOnly, decimal> movedOn = [];
        foreach (BankTransaction transaction in transactions)
        {
            if (transaction.AccountId != account.Id)
            {
                throw new ArgumentException($"A transaction isn't on account {account.Id}.", nameof(transactions));
            }

            if (transaction.IsPending)
            {
                continue;
            }

            DateOnly day = LocalDay(transaction.OccurredAt, timeZone);
            movedOn[day] = movedOn.GetValueOrDefault(day) + transaction.Amount;
        }

        return movedOn;
    }

    /// <summary>
    /// The balance the day before <paramref name="after"/>, keeping the same room between available and current, so
    /// an overdraft or a card's credit left carries back with it.
    /// </summary>
    private static AccountBalance Before(AccountBalance after, decimal moved, DateOnly day)
    {
        decimal current = after.Current - moved;
        decimal room = after.Available - after.Current;
        DateTime endOfDay = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        return new AccountBalance(current + room, current, endOfDay, after.CreditLimit);
    }

    private static DateOnly LocalDay(DateTime utc, TimeZoneInfo timeZone) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone));
}
