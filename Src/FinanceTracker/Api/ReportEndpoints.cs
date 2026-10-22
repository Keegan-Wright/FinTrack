using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Api;

public static class ReportEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IReportService"/> at <see cref="ApiRoutes.Reports"/>.
        /// </summary>
        public RouteGroupBuilder MapReportEndpoints()
        {
            RouteGroupBuilder reports = api.MapGroup(ApiRoutes.Reports);

            reports.MapPost("spent-in-time-period", (FilterTransactions request, IReportService service,
                    CancellationToken cancellationToken) =>
                service.GetSpentInTimePeriodReportAsync(request, cancellationToken));

            reports.MapPost("category-breakdown", (FilterTransactions request, IReportService service,
                    CancellationToken cancellationToken) =>
                service.GetCategoryBreakdownReportAsync(request, cancellationToken));

            reports.MapPost("account-breakdown", (FilterTransactions request, IReportService service,
                    CancellationToken cancellationToken) =>
                service.GetAccountBreakdownReportAsync(request, cancellationToken));

            return api;
        }
    }
}
