using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class UsersEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IUsersService"/> at <see cref="ApiRoutes.Users"/>, admins only. A reset link for a user who
        /// doesn't exist answers 404, for one without a password here 409. Locking or unlocking a user who doesn't
        /// exist answers 404, locking yourself 409.
        /// </summary>
        public RouteGroupBuilder MapUsersEndpoints()
        {
            RouteGroupBuilder users = api.MapGroup(ApiRoutes.Users)
                .RequireAuthorization(policy => policy.RequireRole(FinTrackRoles.Admin));

            users.MapGet("", (IUsersService service, CancellationToken cancellationToken) =>
                service.GetUsersAsync(cancellationToken));

            users.MapPost("{id:guid}/reset-link", async Task<Results<Ok<ResetLink>, Conflict>> (Guid id,
                    IUsersService service, CancellationToken cancellationToken) =>
                {
                    if (await service.CreateResetLinkAsync(id, cancellationToken) is not { } link)
                    {
                        return TypedResults.Conflict();
                    }

                    return TypedResults.Ok(link);
                })
                .NotFoundWhenMissing();

            users.MapPost("{id:guid}/lock", async Task<Results<NoContent, Conflict>> (Guid id, IUsersService service,
                    CancellationToken cancellationToken) =>
                {
                    if (!await service.LockAsync(id, cancellationToken))
                    {
                        return TypedResults.Conflict();
                    }

                    return TypedResults.NoContent();
                })
                .NotFoundWhenMissing();

            users.MapPost("{id:guid}/unlock", async Task<NoContent> (Guid id, IUsersService service,
                    CancellationToken cancellationToken) =>
                {
                    await service.UnlockAsync(id, cancellationToken);
                    return TypedResults.NoContent();
                })
                .NotFoundWhenMissing();

            return api;
        }
    }
}
