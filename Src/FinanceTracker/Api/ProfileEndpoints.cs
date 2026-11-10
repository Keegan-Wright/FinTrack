using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class ProfileEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IProfileService"/> at <see cref="ApiRoutes.Profile"/>. Asking for a confirmation link when
        /// there's nothing to confirm or no mail server, or changing the password or email of an account without a
        /// password, answers 409.
        /// </summary>
        public RouteGroupBuilder MapProfileEndpoints()
        {
            RouteGroupBuilder profile = api.MapGroup(ApiRoutes.Profile);

            profile.MapGet("", async Task<Ok<ProfileDetails>> (IProfileService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetProfileAsync(cancellationToken)));

            profile.MapPost("email/confirmation", async Task<Results<NoContent, Conflict>> (IProfileService service,
                CancellationToken cancellationToken) =>
            {
                if (!await service.SendEmailConfirmationAsync(cancellationToken))
                {
                    return TypedResults.Conflict();
                }

                return TypedResults.NoContent();
            });

            profile.MapPut("password", async Task<Results<Ok<ProfileChange>, Conflict>> (ChangePassword request,
                    IProfileService service, CancellationToken cancellationToken) =>
                    OkOrConflict(await service.ChangePasswordAsync(request, cancellationToken)))
                .Validated<ChangePassword>();

            profile.MapPut("email", async Task<Results<Ok<ProfileChange>, Conflict>> (ChangeEmail request,
                    IProfileService service, CancellationToken cancellationToken) =>
                    OkOrConflict(await service.ChangeEmailAsync(request, cancellationToken)))
                .Validated<ChangeEmail>();

            return api;
        }
    }

    private static Results<Ok<ProfileChange>, Conflict> OkOrConflict(ProfileChange? change)
    {
        if (change is null)
        {
            return TypedResults.Conflict();
        }

        return TypedResults.Ok(change);
    }
}
