using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class BillEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IBillsService"/> at <see cref="ApiRoutes.Bills"/>. Ids that don't exist or aren't the user's,
        /// and accounts that aren't theirs, answer 404. Deleting a bill that can only be ended answers 409.
        /// </summary>
        public RouteGroupBuilder MapBillEndpoints()
        {
            RouteGroupBuilder bills = api.MapGroup(ApiRoutes.Bills);

            bills.MapGet("month/{year:int:range(1,9998)}/{month:int:range(1,12)}",
                async Task<Ok<BillMonthOverview>> (int year, int month, IBillsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.GetMonthAsync(year, month, cancellationToken)));

            bills.MapGet(string.Empty, (IBillsService service, CancellationToken cancellationToken) =>
                service.GetBillsAsync(cancellationToken));

            bills.MapGet("{id:guid}", async Task<Ok<Bill>> (Guid id, IBillsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.GetBillAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            bills.MapPost(string.Empty, async Task<Ok<Bill>> (SaveBill request, IBillsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.AddBillAsync(request, cancellationToken)))
                .Validated<SaveBill>()
                .NotFoundWhenMissing();

            bills.MapPut("{id:guid}", async Task<Ok<Bill>> (Guid id, SaveBill request, IBillsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.UpdateBillAsync(id, request, cancellationToken)))
                .Validated<SaveBill>()
                .NotFoundWhenMissing();

            bills.MapPost("{id:guid}/end", async Task<Ok<Bill>> (Guid id, IBillsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.EndBillAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            bills.MapPost("{id:guid}/reopen", async Task<Ok<Bill>> (Guid id, IBillsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.ReopenBillAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            bills.MapDelete("{id:guid}", async Task<Results<NoContent, ProblemHttpResult>> (Guid id,
                    IBillsService service, CancellationToken cancellationToken) =>
                {
                    try
                    {
                        await service.DeleteBillAsync(id, cancellationToken);
                        return TypedResults.NoContent();
                    }
                    catch (NotSupportedException ex)
                    {
                        return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
                    }
                })
                .NotFoundWhenMissing();

            bills.MapPut("payments/{transactionId:guid}", async Task<Ok<Transaction>> (Guid transactionId,
                        PayBill request, IBillsService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.PayBillAsync(transactionId, request, cancellationToken)))
                .NotFoundWhenMissing();

            bills.MapGet("suggestions", (IBillsService service, CancellationToken cancellationToken) =>
                service.GetSuggestionsAsync(cancellationToken));

            bills.MapGet("suggestions/count", async Task<Ok<int>> (IBillsService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetSuggestionCountAsync(cancellationToken)));

            bills.MapPost("suggestions/{id:guid}/confirm", async Task<Ok<Bill>> (Guid id, IBillsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.ConfirmSuggestionAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            bills.MapPost("suggestions/{id:guid}/dismiss", async Task<NoContent> (Guid id, IBillsService service,
                    CancellationToken cancellationToken) =>
                {
                    await service.DismissSuggestionAsync(id, cancellationToken);
                    return TypedResults.NoContent();
                })
                .NotFoundWhenMissing();

            return api;
        }
    }
}
