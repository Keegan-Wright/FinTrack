using System.Globalization;
using FinanceTracker.Components.Account;
using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FinanceTracker.Extensions;

/// <summary>
/// Confirming it's them for someone who signs in through the identity provider, where a password user would type
/// their password. They're sent to sign in at the provider again, which it does even with a session there, then
/// <see cref="ISingleSignOnConfirmation"/> records it.
/// </summary>
public static class SingleSignOnConfirmationExtensions
{
    /// <summary>
    /// How old the provider's sign in can be when it comes back. It's asked to sign in again, so anything older means
    /// it used its session instead. This is the real check, as not every provider honours being asked.
    /// </summary>
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The challenge's item holding who asked, so the answer can't confirm someone else.
    /// </summary>
    private const string UserItem = "fintrack:confirm_user";

    /// <summary>
    /// The query added to the return address when it didn't confirm them.
    /// </summary>
    private const string FailedQuery = "sso";

    extension(WebApplication app)
    {
        /// <summary>
        /// <c>/auth/oidc/confirm</c> sends the signed in user to sign in at the provider again, asking it with
        /// <c>prompt=login</c> not to rely on its session. <c>/auth/oidc/confirmed</c> checks the provider signed in the same person just now,
        /// records it, and goes back to the return address, with <c>sso=failed</c> when it didn't.
        /// </summary>
        public WebApplication MapSingleSignOnConfirmation()
        {
            app.MapGet("/auth/oidc/confirm", ConfirmAsync).RequireAuthorization();
            app.MapGet("/auth/oidc/confirmed", ConfirmedAsync).RequireAuthorization();
            return app;
        }
    }

    /// <summary>
    /// Sends the user to the provider to sign in again, coming back to <c>/auth/oidc/confirmed</c>.
    /// </summary>
    private static async Task<IResult> ConfirmAsync(HttpContext context, string? returnUrl,
        UserManager<FinanceTrackerUser> userManager, ILogger<SingleSignOnConfirmation> logger)
    {
        string back = AuthenticationExtensions.LocalPath(context.Request, returnUrl);
        FinanceTrackerUser? user = await userManager.GetUserAsync(context.User);
        if (user?.OpenIdConnectSubject is null)
        {
            return TypedResults.Redirect(Failed(back));
        }

        // prompt=login without max_age=0, which Authelia can never satisfy (authelia/authelia#13827). The age of
        // the sign in is checked from auth_time when it comes back instead
        OpenIdConnectChallengeProperties properties = new()
        {
            RedirectUri = $"/auth/oidc/confirmed?returnUrl={Uri.EscapeDataString(back)}",
            Prompt = OpenIdConnectPrompt.Login
        };
        properties.Items[UserItem] = user.Id.ToString();

        try
        {
            await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);
            return TypedResults.Empty;
        }
        catch (Exception exception) when (AuthenticationExtensions.IsProviderFailure(exception))
        {
            logger.LogSingleSignOnNotConfirmed(user.Id, "the provider couldn't be reached");
            return TypedResults.Redirect(Failed(back));
        }
    }

    /// <summary>
    /// Records the confirmation once the provider has signed the same person in just now.
    /// </summary>
    private static async Task<RedirectHttpResult> ConfirmedAsync(HttpContext context, string? returnUrl,
        UserManager<FinanceTrackerUser> userManager, ISingleSignOnConfirmation confirmation, TimeProvider timeProvider,
        ILogger<SingleSignOnConfirmation> logger)
    {
        string back = AuthenticationExtensions.LocalPath(context.Request, returnUrl);
        AuthenticateResult external = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
        await context.SignOutAsync(IdentityConstants.ExternalScheme);

        FinanceTrackerUser? user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            return TypedResults.Redirect(Failed(back));
        }

        string? refusal = RefusalOf(external, user, timeProvider.GetUtcNow());
        if (refusal is not null)
        {
            logger.LogSingleSignOnNotConfirmed(user.Id, refusal);
            return TypedResults.Redirect(Failed(back));
        }

        await confirmation.RecordAsync(user);
        logger.LogSingleSignOnConfirmed(user.Id);
        return TypedResults.Redirect(back);
    }

    /// <summary>
    /// Why the provider's answer doesn't confirm <paramref name="user"/>, null when it does. It has to be for the
    /// sign in they asked for, the same person at the provider, signed in within <see cref="FreshFor"/>.
    /// </summary>
    private static string? RefusalOf(AuthenticateResult external, FinanceTrackerUser user, DateTimeOffset now)
    {
        if (!external.Succeeded)
        {
            return "the provider didn't sign them in";
        }

        if (external.Properties?.Items.TryGetValue(UserItem, out string? askedBy) != true ||
            askedBy != user.Id.ToString())
        {
            return "it wasn't the confirmation they asked for";
        }

        if (external.Principal?.FindFirst("sub")?.Value != user.OpenIdConnectSubject)
        {
            return "someone else signed in at the provider";
        }

        string? authTime = external.Principal?.FindFirst("auth_time")?.Value;
        if (!long.TryParse(authTime, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
        {
            return "the provider didn't say when they signed in";
        }

        TimeSpan age = now - DateTimeOffset.FromUnixTimeSeconds(seconds);
        if (age > FreshFor || age < -FreshFor)
        {
            return "the provider used an earlier sign in";
        }

        return null;
    }

    /// <summary>
    /// <paramref name="back"/> with <c>sso=failed</c>, for the page to say it didn't confirm them.
    /// </summary>
    private static string Failed(string back) => QueryHelpers.AddQueryString(back, FailedQuery, "failed");
}
