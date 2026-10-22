using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Budget;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class BudgetCategoryEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IBudgetCategoriesService"/> at <see cref="ApiRoutes.BudgetCategories"/>.
        /// </summary>
        public RouteGroupBuilder MapBudgetCategoryEndpoints()
        {
            RouteGroupBuilder categories = api.MapGroup(ApiRoutes.BudgetCategories);

            categories.MapGet(string.Empty, (IBudgetCategoriesService service, CancellationToken cancellationToken) =>
                service.GetBudgetItemsAsync(cancellationToken));

            categories.MapPost(string.Empty, async Task<Ok<BudgetCategory>> (AddBudgetCategory request,
                    IBudgetCategoriesService service, CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.AddBudgetCategoryAsync(request, cancellationToken)));

            categories.MapDelete("{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id,
                    IBudgetCategoriesService service, CancellationToken cancellationToken) =>
                await service.DeleteBudgetCategoryAsync(id, cancellationToken)
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound());

            return api;
        }
    }
}
