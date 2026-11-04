using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.PayPeriods;

/// <summary>
/// What the user has coming in like pay, from their transactions over the last
/// <see cref="PayDetector.LookBackMonths"/> months, see <see cref="PayDetector"/>.
/// </summary>
internal static class PayTransactions
{
    /// <summary>
    /// Detects the user's pay, the largest first. Dates are encrypted, so the day index narrows the transactions and
    /// the detector the exact range.
    /// </summary>
    public static async Task<IReadOnlyList<DetectedPay>> DetectAsync(FinanceTrackerContext context, Guid userId,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        DateOnly today = timeProvider.Today;
        DateOnly from = today.AddMonths(-PayDetector.LookBackMonths).AddDays(-PayDetector.SlackDays);

        // A day early for where the user is ahead of UTC
        DateTime fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);

        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(userId)
            .WhereEncryptedDayWithin(x => x.OccurredAt, fromUtc, null, context)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return PayDetector.Detect(transactions, timeProvider.LocalTimeZone, today);
    }
}
