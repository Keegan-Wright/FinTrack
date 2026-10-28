using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class TransferEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="ITransfersService"/> at <see cref="ApiRoutes.Transfers"/>. Each decision answers with the
        /// transactions it changed.
        /// </summary>
        public RouteGroupBuilder MapTransferEndpoints()
        {
            RouteGroupBuilder transfers = api.MapGroup(ApiRoutes.Transfers);

            transfers.MapGet("review", (ITransfersService service, CancellationToken cancellationToken) =>
                service.GetTransfersToReviewAsync(cancellationToken));

            transfers.MapGet("review/count", async Task<Ok<int>> (ITransfersService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetReviewCountAsync(cancellationToken)));

            transfers.MapPost("{id:guid}/transfer", async Task<Ok<IReadOnlyList<Transaction>>> (Guid id,
                        ITransfersService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.MarkAsTransferAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            transfers.MapPost("{id:guid}/not-a-transfer", async Task<Ok<IReadOnlyList<Transaction>>> (Guid id,
                        ITransfersService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.MarkNotATransferAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            transfers.MapDelete("{id:guid}/decision", async Task<Ok<IReadOnlyList<Transaction>>> (Guid id,
                        ITransfersService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.ResetTransferAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            return api;
        }
    }
}
