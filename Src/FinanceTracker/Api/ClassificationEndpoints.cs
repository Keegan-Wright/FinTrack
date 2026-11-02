using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class ClassificationEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IClassificationService"/> at <see cref="ApiRoutes.Classifications"/>. Ids that don't exist or
        /// aren't the user's answer 404, a tag name the user already has answers 409.
        /// </summary>
        public RouteGroupBuilder MapClassificationEndpoints()
        {
            RouteGroupBuilder classifications = api.MapGroup(ApiRoutes.Classifications);

            classifications.MapGet(string.Empty, (IClassificationService service,
                    CancellationToken cancellationToken) =>
                service.GetAllCustomClassificationsAsync(cancellationToken));

            classifications.MapGet("{id:guid}", async Task<Ok<GetClassification>> (Guid id,
                        IClassificationService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.GetClassificationAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            classifications.MapPost(string.Empty, async Task<Ok<Classification>> (AddClassifications request,
                        IClassificationService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.AddCustomClassificationAsync(request, cancellationToken)))
                .Validated<AddClassifications>()
                .ConflictWhenNameTaken();

            classifications.MapPut("{id:guid}", async Task<Ok<Classification>> (Guid id,
                        RenameClassification request, IClassificationService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.RenameCustomClassificationAsync(id, request, cancellationToken)))
                .Validated<RenameClassification>()
                .ConflictWhenNameTaken()
                .NotFoundWhenMissing();

            classifications.MapPost("transactions", async Task<NoContent> (
                    AddCustomClassificationsToTransaction request, IClassificationService service,
                    CancellationToken cancellationToken) =>
                {
                    await service.AddCustomClassificationsToTransactionAsync(request, cancellationToken);
                    return TypedResults.NoContent();
                })
                .Validated<AddCustomClassificationsToTransaction>()
                .NotFoundWhenMissing();

            classifications.MapDelete("{id:guid}", async Task<NoContent> (Guid id, IClassificationService service,
                    CancellationToken cancellationToken) =>
                {
                    await service.RemoveCustomClassificationAsync(id, cancellationToken);
                    return TypedResults.NoContent();
                })
                .NotFoundWhenMissing();

            return api;
        }
    }
}
