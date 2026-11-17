using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Redirects from the statically rendered account pages, with a status message when there's one to show.
/// </summary>
/// <param name="navigationManager">Does the redirect.</param>
internal sealed class IdentityRedirectManager(NavigationManager navigationManager)
{
    /// <summary>
    /// The cookie carrying a status message to the next page.
    /// </summary>
    public const string StatusCookieName = "Identity.StatusMessage";

    private static readonly CookieBuilder StatusCookieBuilder = new()
    {
        SameSite = SameSiteMode.Strict, HttpOnly = true, IsEssential = true, MaxAge = TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// The current page's address without its query.
    /// </summary>
    private string CurrentPath => navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path);

    /// <summary>
    /// Redirects to <paramref name="uri"/>, kept on this site.
    /// </summary>
    /// <param name="uri">Where to, empty for the home page.</param>
    /// <exception cref="InvalidOperationException">It isn't static rendering.</exception>
    [DoesNotReturn]
    public void RedirectTo(string? uri)
    {
        uri ??= "";

        // Prevent open redirects.
        if (!Uri.IsWellFormedUriString(uri, UriKind.Relative))
        {
            uri = navigationManager.ToBaseRelativePath(uri);
        }

        // During static rendering, NavigateTo throws a NavigationException which is handled by the framework as a redirect.
        // So as long as this is called from a statically rendered Identity component, the InvalidOperationException is never thrown.
        navigationManager.NavigateTo(uri);
        throw new InvalidOperationException(
            $"{nameof(IdentityRedirectManager)} can only be used during static rendering.");
    }

    /// <summary>
    /// Redirects to <paramref name="uri"/> with <paramref name="queryParameters"/> as its query.
    /// </summary>
    /// <param name="uri">Where to.</param>
    /// <param name="queryParameters">The query, replacing any it had.</param>
    [DoesNotReturn]
    public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
    {
        string uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
        string newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
        RedirectTo(newUri);
    }

    /// <summary>
    /// Redirects to <paramref name="uri"/>, showing <paramref name="message"/> there.
    /// </summary>
    /// <param name="uri">Where to.</param>
    /// <param name="message">The status message.</param>
    /// <param name="context">The request, which sets the status cookie.</param>
    [DoesNotReturn]
    public void RedirectToWithStatus(string uri, string message, HttpContext context)
    {
        context.Response.Cookies.Append(StatusCookieName, message, StatusCookieBuilder.Build(context));
        RedirectTo(uri);
    }

    /// <summary>
    /// Reloads the current page.
    /// </summary>
    [DoesNotReturn]
    public void RedirectToCurrentPage() => RedirectTo(CurrentPath);

    /// <summary>
    /// Reloads the current page, showing <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The status message.</param>
    /// <param name="context">The request, which sets the status cookie.</param>
    [DoesNotReturn]
    public void RedirectToCurrentPageWithStatus(string message, HttpContext context)
        => RedirectToWithStatus(CurrentPath, message, context);
}
