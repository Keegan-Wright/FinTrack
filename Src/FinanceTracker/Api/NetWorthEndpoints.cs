using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.NetWorth;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class NetWorthEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="INetWorthService"/> at <see cref="ApiRoutes.NetWorth"/>, each taking an optional <c>from</c>
        /// day.
        /// </summary>
        public RouteGroupBuilder MapNetWorthEndpoints()
        {
            RouteGroupBuilder netWorth = api.MapGroup(ApiRoutes.NetWorth);

            netWorth.MapGet(string.Empty, async Task<Ok<NetWorthHistory>> (DateOnly? from, INetWorthService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetNetWorthAsync(from, cancellationToken)));

            netWorth.MapGet("accounts", (DateOnly? from, INetWorthService service,
                    CancellationToken cancellationToken) =>
                service.GetBalanceHistoriesAsync(from, cancellationToken));

            return api;
        }
    }
}
