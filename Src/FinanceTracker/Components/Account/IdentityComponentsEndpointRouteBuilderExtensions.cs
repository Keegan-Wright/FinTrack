using System.Security.Claims;
using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace FinanceTracker.Components.Account;

/// <summary>
/// The endpoints the account pages post to.
/// </summary>
public static class IdentityComponentsEndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps the account endpoints the Identity pages need, under <c>/Account</c>.
        /// </summary>
        public IEndpointConventionBuilder MapAdditionalIdentityEndpoints()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            RouteGroupBuilder accountGroup = endpoints.MapGroup("/Account");

            accountGroup.MapPost("/PerformExternalLogin", (
                HttpContext context,
                [FromServices] SignInManager<FinanceTrackerUser> signInManager,
                [FromForm] string provider,
                [FromForm] string returnUrl) =>
            {
                IEnumerable<KeyValuePair<string, StringValues>> query =
                [
                    new("ReturnUrl", returnUrl)
                ];

                string redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/ExternalLogin",
                    QueryString.Create(query));

                AuthenticationProperties properties =
                    signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return TypedResults.Challenge(properties, [provider]);
            });

            accountGroup.MapPost("/Logout", async (
                ClaimsPrincipal user,
                [FromServices] SignInManager<FinanceTrackerUser> signInManager,
                [FromForm] string returnUrl) =>
            {
                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect($"~/{returnUrl}");
            });

            // The options for signing in with a passkey, asked for by the sign in page's script before the browser
            // asks for one (app.js). Identity keeps them in a cookie for the sign in to check against
            accountGroup.MapPost("/PasskeyRequestOptions", async Task<Results<ContentHttpResult, NotFound, BadRequest>> (
                HttpContext context,
                [FromServices] SignInManager<FinanceTrackerUser> signInManager,
                [FromServices] SignInMethods signInMethods,
                [FromServices] IAntiforgery antiforgery) =>
            {
                // Passkeys sign in alongside passwords, so they're off when passwords are
                if (!signInMethods.Password)
                {
                    return TypedResults.NotFound();
                }

                // The page's antiforgery token, so another site can't start one in the user's browser
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    return TypedResults.BadRequest();
                }

                // No user, so the browser offers any of this site's passkeys
                string options = await signInManager.MakePasskeyRequestOptionsAsync(null);
                return TypedResults.Content(options, "application/json");
            });

            return accountGroup;
        }
    }
}
