using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class DashboardEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IDashboardService"/> at <see cref="ApiRoutes.Dashboard"/>.
        /// </summary>
        public RouteGroupBuilder MapDashboardEndpoints()
        {
            RouteGroupBuilder dashboard = api.MapGroup(ApiRoutes.Dashboard);

            // GET spent?from=...&to=..., round trip ("O") formatted times
            dashboard.MapGet("spent", async Task<Ok<SpentInTimePeriod>> (DateTime from, DateTime to,
                    IDashboardService service, CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetSpentInTimePeriod(from, to, cancellationToken)));

            dashboard.MapGet("upcoming-payments", (int count, IDashboardService service,
                    CancellationToken cancellationToken) =>
                service.GetUpcomingPaymentsAsync(count, cancellationToken));

            return api;
        }
    }
}
