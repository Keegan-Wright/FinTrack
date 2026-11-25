using FinanceTracker.Infrastructure.Account.Tokens;

namespace FinanceTracker.Tokens;

/// <summary>
/// The names personal access tokens go by in authentication, and which requests they're read from.
/// </summary>
public static class PersonalAccessTokenDefaults
{
    /// <summary>
    /// The token's authentication scheme.
    /// </summary>
    public const string Scheme = "PersonalAccessToken";

    /// <summary>
    /// The default scheme, which sends a request with a token to <see cref="Scheme"/> and anything else to the
    /// cookie.
    /// </summary>
    public const string SelectingScheme = "TokenOrCookie";

    /// <summary>
    /// The claim saying what a token may do, only on a principal from a token.
    /// </summary>
    public const string ScopeClaim = "fintrack:token_scope";

    /// <summary>
    /// The claim holding the token's id, for logs.
    /// </summary>
    public const string TokenIdClaim = "fintrack:token_id";

    /// <summary>
    /// How the Authorization header starts.
    /// </summary>
    private const string Bearer = "Bearer ";

    /// <summary>
    /// The paths tokens are read on, besides the endpoints marked for a token's scope. Anywhere else a token is
    /// ignored, so it can't fetch the app's pages.
    /// </summary>
    private static readonly PathString ApiPath = "/api";

    /// <summary>
    /// Whether <paramref name="request"/> carries a FinTrack token where tokens are read, the API or an endpoint marked
    /// for a token's scope, see <see cref="TokenAccessExtensions.UsableWithToken"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    public static bool IsTokenRequest(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TokenFrom(request) is null)
        {
            return false;
        }

        return request.Path.StartsWithSegments(ApiPath, StringComparison.OrdinalIgnoreCase) ||
               request.HttpContext.GetEndpoint()?.Metadata.GetMetadata<TokenScopeMetadata>() is not null;
    }

    /// <summary>
    /// The FinTrack token in <paramref name="request"/>'s Authorization header, null when there's none.
    /// </summary>
    /// <param name="request">The request.</param>
    public static string? TokenFrom(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? header = request.Headers.Authorization;
        if (header is null || !header.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string token = header[Bearer.Length..].Trim();
        if (!token.StartsWith(PersonalAccessTokenFormat.Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return token;
    }
}
