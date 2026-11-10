using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Bills;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IBillSyncService>]
public class BillSyncService : ServiceBase<BillSyncService>, IBillSyncService
{
    /// <summary>
    /// How far back each sync matches, over the transactions already stored. Long enough to see a quarterly bill's
    /// last payment, so its price is followed. The longest quarter (92 days), the matching window either side of a
    /// due date, see <see cref="BillMatcher.WindowDays"/>, and a few days for a missed sync. Yearly bills are matched
    /// over their whole history once, when they're added. Also enough for three monthly payments in a row, see
    /// <see cref="SubscriptionDetector"/>.
    /// </summary>
    public const int RecentDays = 100;

    private readonly TimeProvider _timeProvider;

    public BillSyncService(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<BillSyncService> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    public async Task<BillSyncSummary> SyncAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateOnly today = _timeProvider.Today;
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;

        List<BankAccount> accounts = await context.BankAccounts.OwnedBy(UserId)
            .Include(static x => x.StandingOrders)
            .Include(static x => x.DirectDebits)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<Bill> bills = await context.Bills.OwnedBy(UserId).ToListAsync(cancellationToken);

        BillSeedSummary seeded = BillSeeder.Seed(accounts, bills, today);
        context.Bills.AddRange(seeded.Added);
        bills.AddRange(seeded.Added);

        DateOnly from = MatchFrom(seeded.Added, today);
        List<BankTransaction> transactions = await LoadTransactionsAsync(context, from, cancellationToken);

        int matched = BillMatcher.Match(bills, transactions, timeZone, _timeProvider.UtcNow);
        int pricesChanged = FollowPrices(bills, transactions, from, timeZone, today);

        // After matching, so payments that now pay a bill aren't suggested as one
        List<BillSuggestion> suggestions = await context.BillSuggestions.OwnedBy(UserId).ToListAsync(cancellationToken);

        IReadOnlyList<DetectedSubscription> detected =
            SubscriptionDetector.Detect(bills, transactions, timeZone, today);

        SuggestionChanges suggested = SubscriptionDetector.Suggest(UserId, suggestions, detected);
        context.BillSuggestions.AddRange(suggested.Added);
        context.BillSuggestions.RemoveRange(suggested.Removed);

        await context.SaveChangesAsync(cancellationToken);

        BillSyncSummary summary = new(seeded.Added.Count, seeded.Ended, matched, pricesChanged,
            suggested.Added.Count);

        Logger.LogSyncedBills(UserId, summary.Added, summary.Ended, summary.Matched, summary.PricesChanged,
            summary.Suggested);
        CurrentActivity.Tag(
            (TelemetryTags.BillsAdded, summary.Added),
            (TelemetryTags.BillsEnded, summary.Ended),
            (TelemetryTags.BillsMatched, summary.Matched),
            (TelemetryTags.BillsPricesChanged, summary.PricesChanged),
            (TelemetryTags.SubscriptionsSuggested, summary.Suggested));

        return summary;
    }

    public async Task<int> MatchFromAsync(DateOnly from, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        List<Bill> bills = await context.Bills.OwnedBy(UserId).ToListAsync(cancellationToken);
        List<BankTransaction> transactions = await LoadTransactionsAsync(context, from, cancellationToken);

        int matched = BillMatcher.Match(bills, transactions, _timeProvider.LocalTimeZone, _timeProvider.UtcNow);
        FollowPrices(bills, transactions, from, _timeProvider.LocalTimeZone, _timeProvider.Today);

        await context.SaveChangesAsync(cancellationToken);
        return matched;
    }

    /// <summary>
    /// The recent days, or back to the start of a bill new this run so its history is matched once.
    /// </summary>
    private static DateOnly MatchFrom(IReadOnlyList<Bill> added, DateOnly today)
    {
        DateOnly from = today.AddDays(-RecentDays);
        foreach (Bill bill in added)
        {
            DateOnly start = bill.StartsOn.AddDays(-BillMatcher.WindowDays);
            if (start < from)
            {
                from = start;
            }
        }

        return from;
    }

    /// <summary>
    /// Tracked, so saving writes only the transactions the matcher changed. Dates are encrypted, so the day index
    /// narrows them and the decrypted dates give the exact range.
    /// </summary>
    private async Task<List<BankTransaction>> LoadTransactionsAsync(FinanceTrackerContext context, DateOnly from,
        CancellationToken cancellationToken)
    {
        DateTime fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1);

        List<BankTransaction> transactions = await context.BankTransactions.OwnedBy(UserId)
            .WhereEncryptedDayWithin(x => x.OccurredAt, fromUtc, null, context)
            .ToListAsync(cancellationToken);

        return transactions.Where(x => x.OccurredAt >= fromUtc).ToList();
    }

    /// <summary>
    /// Follows a price change from each bill's latest paid period. Not for standing orders, the bank reports what
    /// their next payment will be, see <see cref="BillSeeder"/>.
    /// </summary>
    /// <returns>How many bills' expected amount changed.</returns>
    private static int FollowPrices(List<Bill> bills, List<BankTransaction> transactions, DateOnly from,
        TimeZoneInfo timeZone, DateOnly today)
    {
        // Periods due near the start may have payments from before it, which weren't loaded
        DateOnly periodsFrom = from.AddDays(BillMatcher.WindowDays);
        int changed = 0;

        foreach (Bill bill in bills)
        {
            if (bill.Source == BillSource.StandingOrder)
            {
                continue;
            }

            BillPeriod? latest = BillCalculator.Periods(bill, transactions, periodsFrom, today, timeZone, today)
                .LastOrDefault(static x => x.PaymentIds.Count > 0);

            if (latest is not null && bill.FollowPrice(latest))
            {
                changed++;
            }
        }

        return changed;
    }
}
