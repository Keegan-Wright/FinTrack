using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Enums;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Response.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Services.Dashboard;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IDashboardService>]
public class DashboardService : ServiceBase<DashboardService>, IDashboardService
{
    private sealed class TransactionLite
    {
        public decimal Amount { get; init; }
        public DateTime TransactionTime { get; init; }
        public string TransactionCategory { get; init; } = string.Empty;
    }

    private sealed class DirectDebitLite
    {
        public decimal PreviousPaymentAmount { get; init; }
        public DateTime PreviousPaymentTimeStamp { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public DashboardService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory,
        ILogger<DashboardService> logger)
        : base(user, financeTrackerContextFactory, logger)
    {
    }

    public async Task<SpentInTimePeriodResponse> GetSpentInTimePeriod(DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime fromUtc = fromDate.AddDays(-1).ToUniversalTime();
        DateTime toUtc = toDate.AddDays(1).ToUniversalTime();

        IQueryable<OpenBankingTransaction> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.Transactions!))
            .AsNoTracking();

        List<TransactionLite> items = await query
            .Select(x => new TransactionLite
            {
                Amount = x.Amount,
                TransactionTime = x.TransactionTime,
                TransactionCategory = x.TransactionCategory
            })
            .ToListAsync(cancellationToken);

        IEnumerable<decimal> amountsInRange = items
            .Where(x => x.TransactionTime >= fromUtc && x.TransactionTime <= toUtc)
            .Where(x => x.TransactionCategory != "TRANSFER")
            .Select(x => x.Amount);

        return new SpentInTimePeriodResponse
        {
            TotalIn = amountsInRange.Where(x => !decimal.IsNegative(x)).Sum(),
            TotalOut = amountsInRange.Where(decimal.IsNegative).Sum()
        };
    }

    public async IAsyncEnumerable<UpcomingPaymentsResponse> GetUpcomingPaymentsAsync(int numberToFetch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<UpcomingPaymentsResponse> upcomingPayments = [];

        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime nowUtc = DateTime.Now.ToUniversalTime();

        List<UpcomingPaymentsResponse> standingOrders = await context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.StandingOrders)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.StandingOrders!))
            .AsNoTracking()
            .Select(x => new UpcomingPaymentsResponse
            {
                Amount = x.NextPaymentAmount,
                PaymentDate = x.NextPaymentDate,
                PaymentName = x.Payee,
                PaymentType = "Standing Order"
            })
            .ToListAsync(cancellationToken);

        upcomingPayments.AddRange(standingOrders.Where(x => x.PaymentDate > nowUtc));

        List<DirectDebitLite> directDebitItems = await context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.DirectDebits)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.DirectDebits!))
            .AsNoTracking()
            .Select(x => new DirectDebitLite
            {
                PreviousPaymentAmount = x.PreviousPaymentAmount,
                PreviousPaymentTimeStamp = x.PreviousPaymentTimeStamp,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);

        // This feels like a hack but we can only request direct debits once a token is generated every ~90 days
        // Unless we want to use a refresh token every login
        var currentMonth = nowUtc.Month;
        upcomingPayments.AddRange(directDebitItems
            .Where(x => x.PreviousPaymentAmount != 0)
            .Select(x => new UpcomingPaymentsResponse
            {
                Amount = x.PreviousPaymentAmount,
                PaymentDate = x.PreviousPaymentTimeStamp.AddMonths((currentMonth - x.PreviousPaymentTimeStamp.Month) + 1),
                PaymentName = x.Name,
                PaymentType = "Direct Debit"
            })
            .Where(x => x.PaymentDate > nowUtc));


        await foreach (UpcomingPaymentsResponse upcomingPayment in upcomingPayments
                           .OrderBy(x => x.PaymentDate)
                           .Take(numberToFetch)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return upcomingPayment;
        }
    }
}
