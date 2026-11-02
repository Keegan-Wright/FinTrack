using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class DebtEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IDebtsService"/> at <see cref="ApiRoutes.Debts"/>. Ids that don't exist or aren't the user's,
        /// and accounts that aren't theirs, answer 404. A forecast for a monthly payment that isn't more than 0
        /// answers 400.
        /// </summary>
        public RouteGroupBuilder MapDebtEndpoints()
        {
            RouteGroupBuilder debts = api.MapGroup(ApiRoutes.Debts);

            debts.MapGet(string.Empty, (IDebtsService service, CancellationToken cancellationToken) =>
                service.GetDebtsAsync(cancellationToken));

            debts.MapGet("{id:guid}", async Task<Ok<DebtDetail>> (Guid id, IDebtsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.GetDebtAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            debts.MapPost(string.Empty, async Task<Ok<Debt>> (SaveDebt request, IDebtsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.AddDebtAsync(request, cancellationToken)))
                .Validated<SaveDebt>()
                .ConflictWhenNameTaken()
                .NotFoundWhenMissing();

            debts.MapPut("{id:guid}", async Task<Ok<Debt>> (Guid id, SaveDebt request, IDebtsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.UpdateDebtAsync(id, request, cancellationToken)))
                .Validated<SaveDebt>()
                .ConflictWhenNameTaken()
                .NotFoundWhenMissing();

            debts.MapDelete("{id:guid}", async Task<NoContent> (Guid id, IDebtsService service,
                    CancellationToken cancellationToken) =>
                {
                    await service.DeleteDebtAsync(id, cancellationToken);
                    return TypedResults.NoContent();
                })
                .NotFoundWhenMissing();

            debts.MapGet("{id:guid}/forecast", async Task<Results<Ok<DebtForecast>, ProblemHttpResult>> (Guid id,
                    decimal monthlyPayment, IDebtsService service, CancellationToken cancellationToken) =>
                {
                    if (monthlyPayment <= 0)
                    {
                        return TypedResults.Problem("The monthly payment must be more than 0.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    return TypedResults.Ok(await service.ForecastAsync(id, monthlyPayment, cancellationToken));
                })
                .NotFoundWhenMissing();

            debts.MapPut("payments/{transactionId:guid}", async Task<Ok<Transaction>> (Guid transactionId,
                        PayDebt request, IDebtsService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.PayDebtAsync(transactionId, request, cancellationToken)))
                .NotFoundWhenMissing();

            return api;
        }
    }
}
