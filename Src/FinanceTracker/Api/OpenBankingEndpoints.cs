using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class OpenBankingEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IOpenBankingService"/> at <see cref="ApiRoutes.OpenBanking"/>. Only connecting banks and
        /// renewing their consent, syncing stays with the background jobs.
        /// </summary>
        public RouteGroupBuilder MapOpenBankingEndpoints()
        {
            RouteGroupBuilder openBanking = api.MapGroup(ApiRoutes.OpenBanking);

            openBanking.MapGet("providers", (IOpenBankingService service, CancellationToken cancellationToken) =>
                service.GetOpenBankingProvidersForClientAsync(cancellationToken));

            openBanking.MapPost("auth-url", async Task<Ok<string>> (ProviderSetupUrl request,
                    IOpenBankingService service, CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.BuildAuthUrlAsync(request, cancellationToken)));

            openBanking.MapPost("connections", async Task<Ok<bool>> (AddVendor request, IOpenBankingService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.AddVendorViaAccessCodeAsync(request, cancellationToken)));

            openBanking.MapGet("connections", (IOpenBankingService service, CancellationToken cancellationToken) =>
                service.GetConnectionsAsync(cancellationToken));

            openBanking.MapPost("connections/{id:guid}/renewal-url", async Task<Ok<string>> (Guid id,
                        IOpenBankingService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.GetRenewalUrlAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            return api;
        }
    }
}
