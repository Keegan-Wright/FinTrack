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
        /// <see cref="ISavingsGoalsService"/> at <see cref="ApiRoutes.SavingsGoals"/>. Ids that don't exist or aren't
        /// the user's, and accounts or tags they can't use, answer 404.
        /// </summary>
        public RouteGroupBuilder MapSavingsGoalEndpoints()
        {
            RouteGroupBuilder goals = api.MapGroup(ApiRoutes.SavingsGoals);

            goals.MapGet(string.Empty, (ISavingsGoalsService service, CancellationToken cancellationToken) =>
                service.GetSavingsGoalsAsync(cancellationToken));

            goals.MapGet("accounts", (ISavingsGoalsService service, CancellationToken cancellationToken) =>
                service.GetSavingsAccountsAsync(cancellationToken));

            goals.MapGet("{id:guid}", async Task<Ok<SavingsGoal>> (Guid id, ISavingsGoalsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.GetSavingsGoalAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            goals.MapPost(string.Empty, async Task<Ok<SavingsGoal>> (SaveSavingsGoal request,
                        ISavingsGoalsService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.AddSavingsGoalAsync(request, cancellationToken)))
                .Validated<SaveSavingsGoal>()
                .NotFoundWhenMissing();

            goals.MapPut("{id:guid}", async Task<Ok<SavingsGoal>> (Guid id, SaveSavingsGoal request,
                        ISavingsGoalsService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.UpdateSavingsGoalAsync(id, request, cancellationToken)))
                .Validated<SaveSavingsGoal>()
                .NotFoundWhenMissing();

            goals.MapDelete("{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id,
                    ISavingsGoalsService service, CancellationToken cancellationToken) =>
                await service.DeleteSavingsGoalAsync(id, cancellationToken)
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound());

            return api;
        }
    }
}
