using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FinanceTracker.Tokens;

/// <summary>
/// Sets up signing in with a personal access token beside Identity's cookie.
/// </summary>
public static class PersonalAccessTokenExtensions
{

    extension(AuthenticationBuilder authentication)
    {
        /// <summary>
        /// The token's scheme, and a default scheme that sends API requests with a token to it and everything else
        /// to Identity's cookie. With <see cref="TokenAccessHandler"/>, which keeps a token to the endpoints opened to
        /// it, and <see cref="ITokenFailureLimiter"/> with its limit. Call after Identity's cookies are added.
        /// </summary>
        /// <param name="failuresPerHour">Tokens one address may have refused an hour before its tokens aren't looked
        /// at.</param>
        public AuthenticationBuilder AddPersonalAccessTokens(
            int failuresPerHour = TokenFailureLimiterOptions.DefaultFailuresPerHour)
        {
            ArgumentNullException.ThrowIfNull(authentication);
            authentication.Services.Configure<TokenFailureLimiterOptions>(options =>
                options.FailuresPerHour = failuresPerHour);
            authentication.Services.AddSingleton<ITokenFailureLimiter, TokenFailureLimiter>();

            authentication.Services.Configure<AuthenticationOptions>(static options =>
                options.DefaultScheme = PersonalAccessTokenDefaults.SelectingScheme);

            authentication.AddPolicyScheme(PersonalAccessTokenDefaults.SelectingScheme, null, static options =>
                options.ForwardDefaultSelector = static context =>
                    PersonalAccessTokenDefaults.IsTokenRequest(context.Request)
                        ? PersonalAccessTokenDefaults.Scheme
                        : IdentityConstants.ApplicationScheme);

            authentication.AddScheme<AuthenticationSchemeOptions, PersonalAccessTokenHandler>(
                PersonalAccessTokenDefaults.Scheme, null);

            authentication.Services.AddSingleton<IAuthorizationHandler, TokenAccessHandler>();
            return authentication;
        }
    }
}
