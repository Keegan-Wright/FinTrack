using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Response.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Dashboard;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IDashboardService>]
public class DashboardService : ServiceBase, IDashboardService
{
    public DashboardService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory)
        : base(user, financeTrackerContextFactory)
    {
    }

    public async Task<SpentInTimePeriodResponse> GetSpentInTimePeriod(DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<OpenBankingTransaction> query = context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.Transactions!))
            .AsNoTracking().Where(x =>
                x.TransactionTime >= fromDate.AddDays(-1).ToUniversalTime() &&
                x.TransactionTime <= toDate.AddDays(1).ToUniversalTime() && x.TransactionCategory != "TRANSFER");
        List<decimal> items = await query.Select(x => x.Amount).ToListAsync(cancellationToken);

        return new SpentInTimePeriodResponse
        {
            TotalIn = items.Where(x => !decimal.IsNegative(x)).Sum(),
            TotalOut = items.Where(decimal.IsNegative).Sum()
        };
    }

    public async IAsyncEnumerable<UpcomingPaymentsResponse> GetUpcomingPaymentsAsync(int numberToFetch,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<UpcomingPaymentsResponse> upcomingPayments = [];
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        upcomingPayments.AddRange(await context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.StandingOrders)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.StandingOrders!))
            .AsNoTracking()
            .Where(x => x.NextPaymentDate > DateTime.Now.ToUniversalTime())
            .Select(x => new UpcomingPaymentsResponse
            {
                Amount = x.NextPaymentAmount,
                PaymentDate = x.NextPaymentDate,
                PaymentName = x.Payee,
                PaymentType = "Standing Order"
            }).ToListAsync(cancellationToken));


        upcomingPayments.AddRange(await context.IsolateToUser(UserId)
            .Include(x => x.Providers)!.ThenInclude(x => x.Accounts)!.ThenInclude(x => x.DirectDebits)
            .SelectMany(x => x.Providers!.SelectMany(c => c.Accounts!).SelectMany(r => r.DirectDebits!))
            .AsNoTracking()
            .Where(x => x.PreviousPaymentAmount != 0)
            .Where(x => x.PreviousPaymentTimeStamp < DateTime.Now.ToUniversalTime())
            .Select(x => new UpcomingPaymentsResponse
            {
                Amount = x.PreviousPaymentAmount,
                PaymentDate = x.PreviousPaymentTimeStamp.AddMonths(1),
                PaymentName = x.Name,
                PaymentType = "Direct Debit"
            }).ToListAsync(cancellationToken));


        await foreach (UpcomingPaymentsResponse upcomingPayment in upcomingPayments
                           .Where(x => x.PaymentDate > DateTime.Now.ToUniversalTime())
                           .OrderBy(x => x.PaymentDate)
                           .Take(numberToFetch)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return upcomingPayment;
        }
    }
}
