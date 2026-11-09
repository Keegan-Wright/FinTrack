using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Tells the current user when an account they watch goes below the amount they set for it.
/// </summary>
/// <remarks>
/// Said as an account goes below, once until it's back above, see <see cref="NotificationSettings.RecordBalance"/>.
/// Keyed by when it went below, so each dip is said once however long it lasts, and pruning can't raise it again.
/// </remarks>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISyncNotices>]
public sealed class BalanceNotices : ServiceBase<BalanceNotices>, ISyncNotices
{
    /// <summary>
    /// Where accounts are seen.
    /// </summary>
    public const string AccountsLink = "Accounts";

    private readonly TimeProvider _timeProvider;
    private readonly INotifier _notifier;

    public BalanceNotices(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<BalanceNotices> logger,
        TimeProvider timeProvider, INotifier notifier)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _notifier = notifier;
    }

    public async Task NotifyAsync(CancellationToken cancellationToken)
    {
        List<(BankAccount Account, decimal Available, LowBalanceAlert Alert)> wentLow = [];
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            NotificationSettings? settings = await context.NotificationSettings.OwnedBy(UserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (settings is null || settings.LowBalanceAlerts.Count == 0)
            {
                return;
            }

            List<BankAccount> accounts = await context.BankAccounts.OwnedBy(UserId).AsNoTracking()
                .ToListAsync(cancellationToken);

            wentLow = RecordBalances(settings, accounts);

            // Saved before telling them, so a failure part way leaves it said at most once
            await context.SaveChangesAsync(cancellationToken);
        }

        foreach ((BankAccount account, decimal available, LowBalanceAlert alert) in wentLow)
        {
            await _notifier.NotifyAsync(NotificationKind.LowBalance,
                $"low-balance:{account.Id:N}:{alert.LowSince:yyyyMMddHHmmss}",
                $"{account.DisplayName} is below {NoticeText.Money(alert.Below)}",
                $"Its available balance is {NoticeText.Money(available)}.", AccountsLink,
                cancellationToken);
        }
    }

    /// <summary>
    /// Records each watched account's available balance.
    /// </summary>
    /// <returns>The accounts that have just gone below, with that balance and their alert as it is now.</returns>
    private List<(BankAccount Account, decimal Available, LowBalanceAlert Alert)> RecordBalances(
        NotificationSettings settings, List<BankAccount> accounts)
    {
        DateTime now = _timeProvider.UtcNow;
        List<(BankAccount Account, decimal Available, LowBalanceAlert Alert)> wentLow = [];

        foreach (BankAccount account in accounts)
        {
            if (account.Balance is not { } balance)
            {
                continue;
            }

            if (settings.RecordBalance(account.Id, balance.Available, now))
            {
                LowBalanceAlert alert = settings.LowBalanceAlerts.Single(x => x.AccountId == account.Id);
                wentLow.Add((account, balance.Available, alert));
            }
        }

        return wentLow;
    }
}
