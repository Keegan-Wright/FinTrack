using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

/// <summary>
/// Maps the profile API.
/// </summary>
public static class ProfileEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IProfileService"/> at <see cref="ApiRoutes.Profile"/>. Asking for a confirmation link when
        /// there's nothing to confirm or no mail server, or changing the password, email or two-factor sign in of an
        /// account without a password, answers 409. So does asking for the authenticator key with two-factor sign in
        /// on.
        /// </summary>
        public RouteGroupBuilder MapProfileEndpoints()
        {
            RouteGroupBuilder profile = api.MapGroup(ApiRoutes.Profile);

            profile.MapGet("", async Task<Ok<ProfileDetails>> (IProfileService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetProfileAsync(cancellationToken)));

            profile.MapPost("email/confirmation", (IProfileService service, CancellationToken cancellationToken) =>
                    service.SendEmailConfirmationAsync(cancellationToken))
                .ConflictWhenNone();

            profile.MapPut("password", (ChangePassword request, IProfileService service,
                    CancellationToken cancellationToken) => service.ChangePasswordAsync(request, cancellationToken))
                .Validated<ChangePassword>()
                .ConflictWhenNone();

            profile.MapPut("email", (ChangeEmail request, IProfileService service,
                    CancellationToken cancellationToken) => service.ChangeEmailAsync(request, cancellationToken))
                .Validated<ChangeEmail>()
                .ConflictWhenNone();

            profile.MapGet("two-factor", (IProfileService service, CancellationToken cancellationToken) =>
                    service.GetTwoFactorAsync(cancellationToken))
                .ConflictWhenNone();

            profile.MapGet("two-factor/authenticator", (IProfileService service,
                    CancellationToken cancellationToken) => service.GetAuthenticatorSetupAsync(cancellationToken))
                .ConflictWhenNone();

            profile.MapPost("two-factor", (EnableAuthenticator request, IProfileService service,
                    CancellationToken cancellationToken) =>
                service.EnableAuthenticatorAsync(request, cancellationToken))
                .Validated<EnableAuthenticator>()
                .ConflictWhenNone();

            profile.MapPost("two-factor/recovery-codes", (ConfirmPassword request, IProfileService service,
                    CancellationToken cancellationToken) => service.MakeRecoveryCodesAsync(request, cancellationToken))
                .Validated<ConfirmPassword>()
                .ConflictWhenNone();

            profile.MapPost("two-factor/authenticator/reset", (ConfirmPassword request, IProfileService service,
                    CancellationToken cancellationToken) => service.ResetAuthenticatorAsync(request, cancellationToken))
                .Validated<ConfirmPassword>()
                .ConflictWhenNone();

            profile.MapPost("two-factor/off", (ConfirmPassword request, IProfileService service,
                    CancellationToken cancellationToken) => service.TurnOffTwoFactorAsync(request, cancellationToken))
                .Validated<ConfirmPassword>()
                .ConflictWhenNone();

            profile.MapDelete("two-factor/browser", async Task<NoContent> (IProfileService service,
                CancellationToken cancellationToken) =>
            {
                await service.ForgetBrowserAsync(cancellationToken);
                return TypedResults.NoContent();
            });

            return api;
        }
    }
}
