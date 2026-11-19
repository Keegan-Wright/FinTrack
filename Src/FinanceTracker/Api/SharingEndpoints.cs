using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

/// <summary>
/// Maps the sharing API.
/// </summary>
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
            RouteGroupBuilder view = api.MapGroup(ApiRoutes.SharingView);

            view.MapGet(string.Empty, async Task<Ok<SharingView>> (ISharingService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetViewAsync(cancellationToken)));

            view.MapPut(string.Empty, async Task<Ok<SharingView>> (ChangeSharingView request, ISharingService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.ChangeViewAsync(request, cancellationToken)));

            links.MapGet(string.Empty, (ISharingService service, CancellationToken cancellationToken) =>
                service.GetLinksAsync(cancellationToken));

            api.MapGet(ApiRoutes.SharingPeople, (ISharingService service, CancellationToken cancellationToken) =>
                service.GetPeopleAsync(cancellationToken));

            links.MapPost(string.Empty, async Task<Ok<InviteOutcome>> (InviteToShare request,
                        ISharingService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.InviteAsync(request, cancellationToken)))
                .Validated<InviteToShare>();

            links.MapPost("{id:guid}/accept", (Guid id, ISharingService service,
                    CancellationToken cancellationToken) => service.AcceptAsync(id, cancellationToken))
                .NotFoundWhenNone();

            links.MapPost("{id:guid}/decline", (Guid id, ISharingService service,
                    CancellationToken cancellationToken) => service.DeclineAsync(id, cancellationToken))
                .NotFoundWhenNone();

            links.MapDelete("{id:guid}", (Guid id, ISharingService service,
                    CancellationToken cancellationToken) => service.EndAsync(id, cancellationToken))
                .NotFoundWhenNone();

            links.MapGet("{id:guid}/shares", (Guid id, ISharingService service,
                    CancellationToken cancellationToken) => service.GetSharesAsync(id, cancellationToken))
                .NotFoundWhenNone();

            links.MapPut("{id:guid}/shares/accounts", async Task<Ok<LinkShares>> (Guid id,
                        ChangeAccountShares request, ISharingService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.ChangeAccountSharesAsync(id, request, cancellationToken)))
                .Validated<ChangeAccountShares>();

            links.MapPut("{id:guid}/shares/items", async Task<Ok<LinkShares>> (Guid id,
                        ChangeItemShares request, ISharingService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.ChangeItemSharesAsync(id, request, cancellationToken)))
                .Validated<ChangeItemShares>();

            links.MapPost("{id:guid}/joint-accounts", async Task<Ok<LinkShares>> (Guid id,
                        MergeJointAccounts request, ISharingService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.MergeJointAccountsAsync(id, request, cancellationToken)))
                .Validated<MergeJointAccounts>();

            links.MapDelete("{id:guid}/joint-accounts/{jointAccountId:guid}", async Task<Ok<LinkShares>> (Guid id,
                    Guid jointAccountId, ISharingService service, CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.SplitJointAccountAsync(id, jointAccountId, cancellationToken)));

            return api;
        }
    }
}
