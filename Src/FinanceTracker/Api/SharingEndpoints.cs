using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class SharingEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="ISharingService"/> at <see cref="ApiRoutes.SharingLinks"/>.
        /// </summary>
        public RouteGroupBuilder MapSharingEndpoints()
        {
            RouteGroupBuilder links = api.MapGroup(ApiRoutes.SharingLinks);

            links.MapGet(string.Empty, (ISharingService service, CancellationToken cancellationToken) =>
                service.GetLinksAsync(cancellationToken));

            links.MapPost(string.Empty, async Task<Ok<InviteOutcome>> (InviteToShare request,
                        ISharingService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.InviteAsync(request, cancellationToken)))
                .Validated<InviteToShare>();

            links.MapPost("{id:guid}/accept", async Task<Results<NoContent, NotFound>> (Guid id,
                    ISharingService service, CancellationToken cancellationToken) =>
                NoContentIfFound(await service.AcceptAsync(id, cancellationToken)));

            links.MapPost("{id:guid}/decline", async Task<Results<NoContent, NotFound>> (Guid id,
                    ISharingService service, CancellationToken cancellationToken) =>
                NoContentIfFound(await service.DeclineAsync(id, cancellationToken)));

            links.MapDelete("{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id,
                    ISharingService service, CancellationToken cancellationToken) =>
                NoContentIfFound(await service.EndAsync(id, cancellationToken)));

            return api;
        }
    }

    private static Results<NoContent, NotFound> NoContentIfFound(bool found)
    {
        if (found)
        {
            return TypedResults.NoContent();
        }

        return TypedResults.NotFound();
    }
}
