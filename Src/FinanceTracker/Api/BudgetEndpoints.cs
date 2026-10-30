using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class BudgetEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IBudgetsService"/> at <see cref="ApiRoutes.Budgets"/>. Ids that don't exist or aren't the user's,
        /// and tags that aren't theirs, answer 404.
        /// </summary>
        public RouteGroupBuilder MapBudgetEndpoints()
        {
            RouteGroupBuilder budgets = api.MapGroup(ApiRoutes.Budgets);

            budgets.MapGet("month/{year:int:range(1,9998)}/{month:int:range(1,12)}",
                async Task<Ok<BudgetMonthOverview>> (int year, int month, IBudgetsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.GetMonthAsync(year, month, cancellationToken)));

            budgets.MapGet(string.Empty, (IBudgetsService service, CancellationToken cancellationToken) =>
                service.GetBudgetsAsync(cancellationToken));

            budgets.MapGet("{id:guid}", async Task<Ok<Budget>> (Guid id, IBudgetsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.GetBudgetAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            budgets.MapPost(string.Empty, async Task<Ok<Budget>> (SaveBudget request, IBudgetsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.AddBudgetAsync(request, cancellationToken)))
                .Validated<SaveBudget>()
                .NotFoundWhenMissing();

            budgets.MapPut("{id:guid}", async Task<Ok<Budget>> (Guid id, SaveBudget request, IBudgetsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.UpdateBudgetAsync(id, request, cancellationToken)))
                .Validated<SaveBudget>()
                .NotFoundWhenMissing();

            budgets.MapDelete("{id:guid}", async Task<NoContent> (Guid id, IBudgetsService service,
                    CancellationToken cancellationToken) =>
                {
                    await service.DeleteBudgetAsync(id, cancellationToken);
                    return TypedResults.NoContent();
                })
                .NotFoundWhenMissing();

            return api;
        }
    }
}
