using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Tells the current user about money going out over the amount they asked to hear about.
/// </summary>
/// <remarks>
/// Money out only, as that's what the user wants to catch. Pending payments are left until they book, as a pending
/// one is replaced by its booked one, which would say it twice. Moves between the user's own accounts aren't
/// spending, so they're left out too.
/// </remarks>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ISyncNotices>]
public sealed class TransactionNotices : ServiceBase<TransactionNotices>, ISyncNotices
{
    /// <summary>
    /// Where transactions are seen.
    /// </summary>
    public const string TransactionsLink = "Transactions";

    /// <summary>
    /// How far back a payment is still said, enough for one that took a few days to book. Well inside
    /// <see cref="NotificationRetention.ReadKeptFor"/>, so a pruned notice isn't raised again.
    /// </summary>
    public const int LookBackDays = 7;

    private readonly TimeProvider _timeProvider;
    private readonly INotifier _notifier;

    public TransactionNotices(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<TransactionNotices> logger,
        TimeProvider timeProvider, INotifier notifier)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _notifier = notifier;
    }

    public async Task NotifyAsync(CancellationToken cancellationToken)
    {
        List<BankTransaction> large;
        Dictionary<Guid, string> accountNames;
        await using (FinanceTrackerContext context =
                     await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken))
        {
            if (await LargeOverAsync(context, cancellationToken) is not { } over)
            {
                return;
            }

            large = await LoadLargeAsync(context, over, cancellationToken);
            accountNames = await context.BankAccounts.OwnedBy(UserId).AsNoTracking()
                .ToDictionaryAsync(static x => x.Id, static x => x.DisplayName, cancellationToken);
        }

        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;
        foreach (BankTransaction transaction in large)
        {
            string from = "";
            if (accountNames.TryGetValue(transaction.AccountId, out string? accountName))
            {
                from = $" from {accountName}";
            }

            await _notifier.NotifyAsync(NotificationKind.LargeTransaction, $"large-transaction:{transaction.Id:N}",
                $"{NoticeText.Money(-transaction.Amount)} paid to {transaction.Description}",
                $"Paid{from} on {NoticeText.Day(transaction.OccurredAt.LocalDayIn(timeZone))}.", TransactionsLink,
                cancellationToken);
        }
    }

    /// <summary>
    /// Booked money out of <paramref name="over"/> or more in the last <see cref="LookBackDays"/>, leaving out
    /// transfers. Dates are encrypted, so the day index narrows them and the decrypted dates give the exact range.
    /// </summary>
    private async Task<List<BankTransaction>> LoadLargeAsync(FinanceTrackerContext context, decimal over,
        CancellationToken cancellationToken)
    {
        DateTime from = _timeProvider.UtcNow.AddDays(-LookBackDays);

        List<BankTransaction> recent = await context.BankTransactions.OwnedBy(UserId)
            .WhereEncryptedDayWithin(x => x.OccurredAt, from.AddDays(-1), null, context)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return recent
            .Where(x => x.OccurredAt >= from && !x.IsPending && x.CountsAsIncomeOrSpending && -x.Amount >= over)
            .ToList();
    }

    /// <summary>
    /// The amount the user hears about payments of, null when they don't want to.
    /// </summary>
    private async Task<decimal?> LargeOverAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        NotificationSettings? settings = await context.NotificationSettings.OwnedBy(UserId).AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return settings?.LargeTransactionOver;
    }
}
