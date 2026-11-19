using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Users;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api;

/// <summary>
/// Maps the users API.
/// </summary>
public static class UsersEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IUsersService"/> at <see cref="ApiRoutes.Users"/>, admins only. A reset link for a user who
        /// doesn't exist answers 404, for one without a password here 409. Locking or unlocking a user who doesn't
        /// exist answers 404, locking an admin, yourself included, 409. Giving or taking the admin role answers how it
        /// went, see <see cref="AdminChange"/>.
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
            });

            users.MapPost("{id:guid}/lock", async Task<Results<NoContent, Conflict>> (Guid id, IUsersService service,
                CancellationToken cancellationToken) =>
            {
                if (!await service.LockAsync(id, cancellationToken))
                {
                    return TypedResults.Conflict();
                }

                return TypedResults.NoContent();
            });

            users.MapPost("{id:guid}/unlock", async Task<NoContent> (Guid id, IUsersService service,
                CancellationToken cancellationToken) =>
            {
                await service.UnlockAsync(id, cancellationToken);
                return TypedResults.NoContent();
            });

            users.MapPost("{id:guid}/admin", async Task<Ok<AdminChange>> (Guid id, IUsersService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GrantAdminAsync(id, cancellationToken)));

            users.MapDelete("{id:guid}/admin", async Task<Ok<AdminChange>> (Guid id, IUsersService service,
                [FromServices] ICurrentUser currentUser, [FromServices] UserManager<FinanceTrackerUser> userManager,
                [FromServices] SignInManager<FinanceTrackerUser> signInManager, CancellationToken cancellationToken) =>
            {
                AdminChange change = await service.RemoveAdminAsync(id, cancellationToken);

                // An admin who gave up the role has it gone from their own sign in now
                if (change == AdminChange.Changed && id == currentUser.UserId &&
                    await userManager.FindByIdAsync(id.ToString()) is { } you)
                {
                    await signInManager.RefreshSignInAsync(you);
                }

                return TypedResults.Ok(change);
            });

            return api;
        }
    }
}
