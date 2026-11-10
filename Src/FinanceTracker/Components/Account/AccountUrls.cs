using FinanceTracker.Infrastructure.Notifications;
using Microsoft.AspNetCore.WebUtilities;

namespace FinanceTracker.Components.Account;

/// <summary>
/// Builds the full URLs of account pages, such as a password reset, for the emails that carry them. Under the app's
/// public address when it's set (<c>FINTRACK_PUBLIC_URL</c>), otherwise under the address the request came in on.
/// </summary>
public sealed class AccountUrls
{
    private readonly PublicAddress _publicAddress;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountUrls(PublicAddress publicAddress, IHttpContextAccessor httpContextAccessor)
    {
        _publicAddress = publicAddress;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// The full URL of an account page with its query.
    /// </summary>
    /// <param name="path">The page, relative to the app, e.g. <c>Account/ConfirmEmail</c>.</param>
    /// <param name="query">What the page is given, escaped here.</param>
    /// <exception cref="InvalidOperationException">Outside a request without a public address.</exception>
    public Uri Create(string path, IDictionary<string, string?> query) =>
        new(QueryHelpers.AddQueryString(new Uri(AppAddress(), path).AbsoluteUri, query));

    private Uri AppAddress()
    {
        if (_publicAddress.Url is { } url)
        {
            return url;
        }

        HttpRequest request = _httpContextAccessor.HttpContext?.Request
                              ?? throw new InvalidOperationException("There's no request to link from");
        return new Uri($"{request.Scheme}://{request.Host}{request.PathBase}/");
    }
}
