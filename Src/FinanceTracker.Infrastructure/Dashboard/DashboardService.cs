using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Dashboard;

[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<IDashboardService>]
public class DashboardService : ServiceBase<DashboardService>, IDashboardService
{
    private sealed class TransactionLite
    {
        public decimal Amount { get; init; }
        public DateTime TransactionTime { get; init; }
        public bool IsTransfer { get; init; }
    }

    /// <summary>
    /// How far back payments show, so one just paid is ticked off rather than gone, and a late one stays.
    /// </summary>
    private const int RecentDays = 7;

    /// <summary>
    /// How far ahead payments show, enough to reach the next of every monthly bill.
    /// </summary>
    private const int AheadDays = 45;

    private readonly TimeProvider _timeProvider;
    private readonly IAccountScope _accountScope;

    public DashboardService(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<DashboardService> logger, TimeProvider timeProvider, IAccountScope accountScope)
        : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
        _accountScope = accountScope;
    }

    public async Task<SpentInTimePeriod> GetSpentInTimePeriod(DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime fromUtc = fromDate.ToUniversalTime();
        DateTime toUtc = toDate.ToUniversalTime();

        AccountsInView view = await _accountScope.ResolveAsync(context, cancellationToken);
        List<TransactionLite> items = await context.BankTransactions.InView(view)
            .WhereEncryptedDayBetween(x => x.OccurredAt, fromUtc, toUtc, context)
            .Select(x => new TransactionLite
            {
                Amount = x.Amount,
                TransactionTime = x.OccurredAt,
                // BankTransaction.CountsAsIncomeOrSpending, in a form the database can read
                IsTransfer = x.Transfer != null && x.Transfer.Decision == TransferDecision.Transfer
            })
            .ToListAsync(cancellationToken);

        List<TransactionLite> inRange = items
            .Where(x => x.TransactionTime >= fromUtc && x.TransactionTime < toUtc)
            .ToList();

        List<decimal> counted = inRange.Where(static x => !x.IsTransfer).Select(static x => x.Amount).ToList();
        List<decimal> transferred = inRange.Where(static x => x.IsTransfer).Select(static x => x.Amount).ToList();

        return new SpentInTimePeriod
        {
            TotalIn = counted.Where(static x => !decimal.IsNegative(x)).Sum(),
            TotalOut = counted.Where(decimal.IsNegative).Sum(),
            TransferredIn = transferred.Where(static x => !decimal.IsNegative(x)).Sum(),
            TransferredOut = transferred.Where(decimal.IsNegative).Sum()
        };
    }

    public async IAsyncEnumerable<UpcomingPayment> GetUpcomingPaymentsAsync(int numberToFetch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateOnly today = _timeProvider.Today;
        DateOnly from = today.AddDays(-RecentDays);
        DateOnly to = today.AddDays(AheadDays);
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;

        List<Bill> bills = await context.Bills.OwnedBy(UserId).AsNoTracking().ToListAsync(cancellationToken);
        List<BankTransaction> payments = await context.BankTransactions.OwnedBy(UserId).AsNoTracking()
            .Where(static x => x.BillPayment != null && x.BillPayment.BillId != null)
            .ToListAsync(cancellationToken);

        List<UpcomingPayment> upcoming = [];
        foreach (Bill bill in bills)
        {
            foreach (BillPeriod period in BillCalculator.Periods(bill, payments, from, to, timeZone, today))
            {
                upcoming.Add(ToUpcoming(bill, period));
            }
        }

        foreach (UpcomingPayment payment in upcoming.OrderBy(static x => x.Due).Take(numberToFetch))
        {
            yield return payment;
        }
    }

    private static UpcomingPayment ToUpcoming(Bill bill, BillPeriod period)
    {
        decimal amount = period.Expected;
        if (period.PaymentIds.Count > 0)
        {
            amount = period.Paid;
        }

        return new UpcomingPayment
        {
            BillId = bill.Id,
            PaymentName = bill.Name,
            Amount = amount,
            Due = period.Due,
            State = Enum.Parse<Contracts.Bills.BillState>(period.State.ToString()),
            PaymentType = SourceName(bill.Source)
        };
    }

    private static string SourceName(BillSource source) => source switch
    {
        BillSource.StandingOrder => "Standing order",
        BillSource.DirectDebit => "Direct debit",
        BillSource.Detected => "Subscription",
        _ => "Bill"
    };
}
