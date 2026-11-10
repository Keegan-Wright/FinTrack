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
        /// there's nothing to confirm or no mail server answers 409.
        /// </summary>
        public RouteGroupBuilder MapProfileEndpoints()
        {
            RouteGroupBuilder profile = api.MapGroup(ApiRoutes.Profile);

            profile.MapGet("email", async Task<Ok<EmailStatus>> (IProfileService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetEmailStatusAsync(cancellationToken)));

            profile.MapPost("email/confirmation", async Task<Results<NoContent, Conflict>> (IProfileService service,
                CancellationToken cancellationToken) =>
            {
                if (!await service.SendEmailConfirmationAsync(cancellationToken))
                {
                    return TypedResults.Conflict();
                }

                return TypedResults.NoContent();
            });

            return api;
        }
    }
}
