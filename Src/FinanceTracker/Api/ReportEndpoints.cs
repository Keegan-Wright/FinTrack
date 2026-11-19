using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tokens;

namespace FinanceTracker.Api;

/// <summary>
/// Maps the report API.
/// </summary>
public static class ReportEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IReportService"/> at <see cref="ApiRoutes.Reports"/>.
        /// </summary>
        public RouteGroupBuilder MapReportEndpoints()
        {
            RouteGroupBuilder reports = api.MapGroup(ApiRoutes.Reports)
                .ReadableWithToken();

            reports.MapPost("spent-in-time-period", (FilterTransactions request, IReportService service,
                    CancellationToken cancellationToken) =>
                service.GetSpentInTimePeriodReportAsync(request, cancellationToken))
                .ReadsOnly();

            reports.MapPost("category-breakdown", (FilterTransactions request, IReportService service,
                    CancellationToken cancellationToken) =>
                service.GetCategoryBreakdownReportAsync(request, cancellationToken))
                .ReadsOnly();

            reports.MapPost("account-breakdown", (FilterTransactions request, IReportService service,
                    CancellationToken cancellationToken) =>
                service.GetAccountBreakdownReportAsync(request, cancellationToken))
                .ReadsOnly();

            return api;
        }
    }
}
