using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class RuleEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IRulesService"/> at <see cref="ApiRoutes.Rules"/>. Ids that don't exist or aren't the user's,
        /// and tags or accounts that aren't theirs, answer 404.
        /// </summary>
        public RouteGroupBuilder MapRuleEndpoints()
        {
            RouteGroupBuilder rules = api.MapGroup(ApiRoutes.Rules);

            rules.MapGet(string.Empty, (IRulesService service, CancellationToken cancellationToken) =>
                service.GetRulesAsync(cancellationToken));

            rules.MapGet("{id:guid}", async Task<Ok<Rule>> (Guid id, IRulesService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.GetRuleAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            rules.MapPost(string.Empty, async Task<Ok<Rule>> (SaveRule request, IRulesService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.AddRuleAsync(request, cancellationToken)))
                .Validated<SaveRule>()
                .NotFoundWhenMissing();

            rules.MapPut("{id:guid}", async Task<Ok<Rule>> (Guid id, SaveRule request, IRulesService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.UpdateRuleAsync(id, request, cancellationToken)))
                .Validated<SaveRule>()
                .NotFoundWhenMissing();

            rules.MapPut("order", async Task<NoContent> (ReorderRules request, IRulesService service,
                    CancellationToken cancellationToken) =>
                {
                    await service.ReorderRulesAsync(request, cancellationToken);
                    return TypedResults.NoContent();
                })
                .Validated<ReorderRules>()
                .NotFoundWhenMissing();

            rules.MapPost("preview", async Task<Ok<RulePreview>> (SaveRule request, IRulesService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.PreviewRuleAsync(null, request, cancellationToken)))
                .Validated<SaveRule>()
                .NotFoundWhenMissing();

            rules.MapPost("{id:guid}/preview", async Task<Ok<RulePreview>> (Guid id, SaveRule request,
                        IRulesService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.PreviewRuleAsync(id, request, cancellationToken)))
                .Validated<SaveRule>()
                .NotFoundWhenMissing();

            rules.MapPost("{id:guid}/apply", async Task<Ok<int>> (Guid id, IRulesService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.ApplyRuleToExistingAsync(id, cancellationToken)))
                .NotFoundWhenMissing();

            rules.MapGet("draft/{transactionId:guid}", async Task<Ok<SaveRule>> (Guid transactionId,
                        IRulesService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.GetDraftFromTransactionAsync(transactionId, cancellationToken)))
                .NotFoundWhenMissing();

            rules.MapDelete("{id:guid}", async Task<NoContent> (Guid id, IRulesService service,
                    CancellationToken cancellationToken) =>
                {
                    await service.DeleteRuleAsync(id, cancellationToken);
                    return TypedResults.NoContent();
                })
                .NotFoundWhenMissing();

            return api;
        }
    }
}
