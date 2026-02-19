using System.Runtime.CompilerServices;
using System.Security.Claims;
using FinanceTracker.Data;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.Response.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Services.Dashboard;

[InjectionCategory(InjectionCategoryType.Service)]
[Scoped<IDashboardService>]
public class DashboardService : ServiceBase, IDashboardService
{
    public DashboardService(ClaimsPrincipal user, IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory) : base(user, financeTrackerContextFactory)
    {
    }

    public async Task<SpentInTimePeriodResponse> GetSpentInTimePeriod(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.IsolateToUser(UserId)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.Transactions)
            .SelectMany(x => x.Providers.SelectMany(c => c.Accounts).SelectMany(r => r.Transactions))
            .AsNoTracking().Where(x => (x.TransactionTime >= from.AddDays(-1).ToUniversalTime() && x.TransactionTime <= to.AddDays(1).ToUniversalTime()) && x.TransactionCategory != "TRANSFER");
        var items = await query.Select(x => x.Amount).ToListAsync(cancellationToken: cancellationToken);

        return new SpentInTimePeriodResponse()
        {
            TotalIn = items.Where(x => !decimal.IsNegative(x)).Sum(),
            TotalOut = items.Where(x => decimal.IsNegative(x)).Sum()
        };
    }

    public async IAsyncEnumerable<UpcomingPaymentsResponse> GetUpcomingPaymentsAsync(int numberToFetch, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var upcomingPayments = new List<UpcomingPaymentsResponse>();
        await using var context = await _financeTrackerContextFactory.CreateDbContextAsync(cancellationToken);
        upcomingPayments.AddRange(await context.IsolateToUser(UserId)
            .Include(x=> x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.StandingOrders)
            .SelectMany(x => x.Providers.SelectMany(c => c.Accounts).SelectMany(r => r.StandingOrders))
            .AsNoTracking()
            .Where(x => x.NextPaymentDate > DateTime.Now.ToUniversalTime())
            .Select(x => new UpcomingPaymentsResponse()
            {
                Amount = x.NextPaymentAmount,
                PaymentDate = x.NextPaymentDate,
                PaymentName = x.Payee,
                PaymentType = "Standing Order"
            }).ToListAsync(cancellationToken: cancellationToken));


        upcomingPayments.AddRange(await context.IsolateToUser(UserId)
            .Include(x => x.Providers).ThenInclude(x => x.Accounts).ThenInclude(x => x.DirectDebits)
            .SelectMany(x => x.Providers.SelectMany(c => c.Accounts).SelectMany(r => r.DirectDebits))
            .AsNoTracking()
            .Where(x => x.PreviousPaymentAmount != 0)
            .Where(x => x.PreviousPaymentTimeStamp < DateTime.Now.ToUniversalTime())
            .Select(x => new UpcomingPaymentsResponse()
            {
                Amount = x.PreviousPaymentAmount,
                PaymentDate = x.PreviousPaymentTimeStamp.AddMonths(1),
                PaymentName = x.Name,
                PaymentType = "Direct Debit"
            }).ToListAsync(cancellationToken: cancellationToken));


        await foreach (var upcomingPayment in upcomingPayments
                           .Where(x => x.PaymentDate > DateTime.Now.ToUniversalTime())
                           .OrderBy(x => x.PaymentDate)
                           .Take(numberToFetch)
                           .ToAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return upcomingPayment;
        }
    }
}