using FinanceTracker.Tests.Integration.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// What a browser does for single sign-on against <see cref="FakeOidcProvider"/>, for the tests that sign in or
/// confirm through it.
/// </summary>
public static class SingleSignOnSteps
{
    /// <summary>
    /// Follows <paramref name="path"/>, which sends them to the provider.
    /// </summary>
    /// <returns>The provider authorisation request's parameters, such as its state, nonce and prompt.</returns>
    public static async Task<Dictionary<string, StringValues>> ChallengeAsync(FinTrackClient client, string path,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage challenge = await client.GetAsync(path, cancellationToken);
        Uri provider = challenge.Headers.Location
                       ?? throw new InvalidOperationException($"{path} answered {challenge.StatusCode}");
        return QueryHelpers.ParseQuery(provider.Query);
    }

    /// <summary>
    /// The person signs in and agrees at the provider, which posts back to the app's handler, then the browser
    /// follows the app on to its own <c>/auth/oidc/</c> endpoint, when it's sent there.
    /// </summary>
    /// <param name="authorize">The provider authorisation request's parameters, from <see cref="ChallengeAsync"/>.
    /// </param>
    /// <param name="authTime">When they signed in at the provider, now unless given.</param>
    /// <returns>Where the app sends them once it's done.</returns>
    public static async Task<HttpResponseMessage> FinishAsync(FinTrackClient client, FakeOidcProvider provider,
        Dictionary<string, StringValues> authorize, FakeOidcUser person, CancellationToken cancellationToken,
        DateTimeOffset? authTime = null)
    {
        string code = Guid.NewGuid().ToString("N");
        provider.Approve(code, authorize["nonce"].ToString(), person, authTime);

        HttpResponseMessage returned = await PostToHandlerAsync(client, authorize, new Dictionary<string, string>
        {
            ["code"] = code,
            ["state"] = authorize["state"].ToString()
        }, cancellationToken);

        // A failure the handler catches goes straight to the sign in page, success goes on to the app's endpoint
        string next = returned.Headers.Location?.ToString() ?? string.Empty;
        if (!next.StartsWith("/auth/oidc/", StringComparison.Ordinal))
        {
            return returned;
        }

        return await client.GetAsync(next, cancellationToken);
    }

    /// <summary>
    /// The provider sending them back to the app's OIDC handler, as a form post.
    /// </summary>
    public static async Task<HttpResponseMessage> PostToHandlerAsync(FinTrackClient client,
        Dictionary<string, StringValues> authorize, Dictionary<string, string> answer,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, authorize["redirect_uri"].ToString())
        {
            Content = new FormUrlEncodedContent(answer)
        };
        return await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
    }
}
