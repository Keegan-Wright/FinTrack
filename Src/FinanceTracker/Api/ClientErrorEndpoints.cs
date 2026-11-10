using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Diagnostics;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class ClientErrorEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IClientErrorsService"/> at <see cref="ApiRoutes.ClientErrors"/>, signed in only like the rest
        /// of the API so it can't be used to fill the logs from outside.
        /// </summary>
        public RouteGroupBuilder MapClientErrorEndpoints()
        {
            api.MapPost(ApiRoutes.ClientErrors, async Task<NoContent> (ClientError error,
                    IClientErrorsService service, CancellationToken cancellationToken) =>
                {
                    await service.ReportAsync(error, cancellationToken);
                    return TypedResults.NoContent();
                })
                .Validated<ClientError>();

            return api;
        }
    }
}
