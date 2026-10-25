using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
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
    }

    private sealed class DirectDebitLite
    {
        public decimal PreviousPaymentAmount { get; init; }
        public DateTime PreviousPaymentTimeStamp { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private readonly TimeProvider _timeProvider;

    public DashboardService(ICurrentUser currentUser, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<DashboardService> logger, TimeProvider timeProvider)
        : base(currentUser, financeTrackerContextFactory, logger) =>
        _timeProvider = timeProvider;

    public async Task<SpentInTimePeriod> GetSpentInTimePeriod(DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime fromUtc = fromDate.ToUniversalTime();
        DateTime toUtc = toDate.ToUniversalTime();

        List<TransactionLite> items = await context.BankTransactions.OwnedBy(UserId)
            .Select(x => new TransactionLite
            {
                Amount = x.Amount,
                TransactionTime = x.OccurredAt
            })
            .ToListAsync(cancellationToken);

        IEnumerable<decimal> amountsInRange = items
            .Where(x => x.TransactionTime >= fromUtc && x.TransactionTime < toUtc)
            .Select(x => x.Amount);

        return new SpentInTimePeriod
        {
            TotalIn = amountsInRange.Where(x => !decimal.IsNegative(x)).Sum(),
            TotalOut = amountsInRange.Where(decimal.IsNegative).Sum()
        };
    }

    public async IAsyncEnumerable<UpcomingPayment> GetUpcomingPaymentsAsync(int numberToFetch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<UpcomingPayment> upcomingPayments = [];

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        List<UpcomingPayment> standingOrders = await context.BankAccounts.OwnedBy(UserId)
            .SelectMany(static x => x.StandingOrders)
            .Select(x => new UpcomingPayment
            {
                Amount = x.NextPaymentAmount,
                PaymentDate = x.NextPaymentDate,
                PaymentName = x.Payee,
                PaymentType = "Standing Order"
            })
            .ToListAsync(cancellationToken);

        upcomingPayments.AddRange(standingOrders.Where(x => x.PaymentDate > nowUtc));

        List<DirectDebitLite> directDebitItems = await context.BankAccounts.OwnedBy(UserId)
            .SelectMany(static x => x.DirectDebits)
            .Select(x => new DirectDebitLite
            {
                PreviousPaymentAmount = x.PreviousPaymentAmount,
                PreviousPaymentTimeStamp = x.PreviousPaymentDate,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);

        // This feels like a hack but we can only request direct debits once a token is generated every ~90 days
        // Unless we want to use a refresh token every login
        upcomingPayments.AddRange(directDebitItems
            .Where(x => x.PreviousPaymentAmount != 0)
            .Select(x => new UpcomingPayment
            {
                Amount = x.PreviousPaymentAmount,
                PaymentDate = NextMonthlyPaymentAfter(x.PreviousPaymentTimeStamp, nowUtc),
                PaymentName = x.Name,
                PaymentType = "Direct Debit"
            }));


        await foreach (UpcomingPayment upcomingPayment in upcomingPayments
                           .OrderBy(x => x.PaymentDate)
                           .Take(numberToFetch)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return upcomingPayment;
        }
    }

    /// <summary>
    /// The first monthly anniversary of <paramref name="previousPayment"/> after <paramref name="now"/>.
    /// Always offset from the previous payment so short months don't drift the day.
    /// </summary>
    private static DateTime NextMonthlyPaymentAfter(DateTime previousPayment, DateTime now)
    {
        int monthsSince = ((now.Year - previousPayment.Year) * 12) + now.Month - previousPayment.Month;
        DateTime next = previousPayment.AddMonths(Math.Max(monthsSince, 0));

        return next > now ? next : previousPayment.AddMonths(Math.Max(monthsSince, 0) + 1);
    }
}
