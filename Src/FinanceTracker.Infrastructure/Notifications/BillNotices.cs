using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Infrastructure.Bills;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Tells the current user about their bills: one gone unpaid past its due date, one paid a different amount than
/// expected, and one coming up within the days they asked to be reminded. Those shared with them too, see
/// <see cref="SharedBills"/>, by their own settings, whichever view they chose.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISyncNotices>]
public sealed class BillNotices : ServiceBase<BillNotices>, ISyncNotices
{
    /// <summary>
    /// Where bills are dealt with.
    /// </summary>
    public const string BillsLink = "Bills";

    /// <summary>
    /// How far back a late or changed payment is still said. Well inside
    /// <see cref="NotificationRetention.ReadKeptFor"/>, so a pruned notice isn't raised again.
    /// </summary>
    public const int LookBackDays = 30;

    private readonly TimeProvider _timeProvider;
    private readonly INotifier _notifier;

    public BillNotices(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<BillNotices> logger, TimeProvider timeProvider, INotifier notifier)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _notifier = notifier;
    }

    public async Task NotifyAsync(CancellationToken cancellationToken)
    {
        DateOnly today = _timeProvider.Today;
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;

        List<VisibleItem<Bill>> bills;
        List<BankTransaction> payments;
        Dictionary<Guid, BankAccount> accounts;
        int reminderDays;
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            AccountsInView all = await AccountScope.ResolveAllForAsync(context, UserId, cancellationToken);
            bills = await SharedBills.ForAsync(context, UserId, all, cancellationToken);
            payments = await SharedBills.PaymentsAsync(context, bills, cancellationToken);
            accounts = await context.BankAccounts.InView(all).AsNoTracking()
                .ToDictionaryAsync(static x => x.Id, cancellationToken);
            reminderDays = await ReminderDaysAsync(context, cancellationToken);
        }

        foreach (VisibleItem<Bill> bill in bills)
        {
            // Null for a bill shared with them paid from an account they can't see, which isn't named
            BankAccount? account = accounts.GetValueOrDefault(bill.Item.AccountId);
            await NotifyPastAsync(bill, account, payments, today, timeZone, cancellationToken);
            await RemindAsync(bill, account, payments, reminderDays, today, timeZone, cancellationToken);
        }
    }

    /// <summary>
    /// Says each recent period that went unpaid or was paid a different amount. Only periods due since the bill was
    /// added, so adding one doesn't bring up its past.
    /// </summary>
    private async Task NotifyPastAsync(VisibleItem<Bill> visible, BankAccount? account, List<BankTransaction> payments,
        DateOnly today, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        Bill bill = visible.Item;
        string name = NoticeText.Named(bill.Name, visible.SharedBy);
        DateOnly from = today.AddDays(-LookBackDays);
        DateOnly added = bill.Created.LocalDayIn(timeZone);
        if (added > from)
        {
            from = added;
        }

        foreach (BillPeriod period in BillCalculator.Periods(bill, payments, from, today, timeZone, today))
        {
            if (period.State == BillState.Late)
            {
                await _notifier.NotifyAsync(NotificationKind.BillLate, Key("bill-late", bill, period.Due),
                    $"{name} hasn't been paid",
                    $"{NoticeText.Money(period.Expected)} was due {FromAccount(account)}on {NoticeText.Day(period.Due)}.",
                    BillsLink, cancellationToken);
            }

            if (period.State == BillState.Changed)
            {
                await _notifier.NotifyAsync(NotificationKind.BillChanged, Key("bill-changed", bill, period.Due),
                    $"{name} was {NoticeText.Money(period.Paid)}",
                    $"{NoticeText.Money(period.Expected)} was expected for {NoticeText.Day(period.Due)}.",
                    BillsLink, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Reminds them of each period due within <paramref name="reminderDays"/> that isn't paid yet, saying when the
    /// account's available balance won't cover it.
    /// </summary>
    private async Task RemindAsync(VisibleItem<Bill> visible, BankAccount? account, List<BankTransaction> payments,
        int reminderDays, DateOnly today, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        Bill bill = visible.Item;
        DateOnly to = today.AddDays(reminderDays);
        foreach (BillPeriod period in BillCalculator.Periods(bill, payments, today, to, timeZone, today))
        {
            if (period.State != BillState.Upcoming)
            {
                continue;
            }

            string message = $"{NoticeText.Money(period.Expected)} goes out {FromAccount(account)}on " +
                             $"{NoticeText.Day(period.Due)}.{WontCover(account, period.Expected)}";

            await _notifier.NotifyAsync(NotificationKind.BillDueSoon, Key("bill-due", bill, period.Due),
                $"{NoticeText.Named(bill.Name, visible.SharedBy)} is due {NoticeText.DueIn(period.Due, today)}",
                message, BillsLink, cancellationToken);
        }
    }

    /// <summary>
    /// Where it's paid from with a space after, e.g. <c>from Everyday </c>, nothing when the account is gone.
    /// </summary>
    private static string FromAccount(BankAccount? account)
    {
        if (account is null)
        {
            return "";
        }

        return $"from {account.DisplayName} ";
    }

    /// <summary>
    /// A warning when the account's available balance is less than <paramref name="expected"/>, nothing otherwise.
    /// </summary>
    private static string WontCover(BankAccount? account, decimal expected)
    {
        if (account?.Balance is not { } balance || balance.Available >= expected)
        {
            return "";
        }

        return $" Its available balance of {NoticeText.Money(balance.Available)} won't cover it.";
    }

    /// <summary>
    /// Keyed by the bill and the due date, so each period is said once.
    /// </summary>
    private static string Key(string what, Bill bill, DateOnly due) => $"{what}:{bill.Id:N}:{due:yyyyMMdd}";

    private async Task<int> ReminderDaysAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        NotificationSettings? settings = await context.NotificationSettings.OwnedBy(UserId).AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return settings?.BillReminderDays ?? NotificationSettings.DefaultBillReminderDays;
    }
}
