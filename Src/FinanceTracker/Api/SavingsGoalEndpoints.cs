using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class SavingsGoalEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="ISavingsGoalsService"/> at <see cref="ApiRoutes.SavingsGoals"/>.
        /// </summary>
        public RouteGroupBuilder MapSavingsGoalEndpoints()
        {
            RouteGroupBuilder goals = api.MapGroup(ApiRoutes.SavingsGoals);

            goals.MapGet(string.Empty, (ISavingsGoalsService service, CancellationToken cancellationToken) =>
                service.GetSavingsGoalsAsync(cancellationToken));

            goals.MapPost(string.Empty, async Task<Ok<SavingsGoal>> (AddSavingsGoal request,
                    ISavingsGoalsService service, CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.AddSavingsGoalAsync(request, cancellationToken)));

            goals.MapDelete("{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id,
                    ISavingsGoalsService service, CancellationToken cancellationToken) =>
                await service.DeleteSavingsGoalAsync(id, cancellationToken)
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound());

            return api;
        }
    }
}
