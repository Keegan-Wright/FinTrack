using System.Net;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Users;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IUsersService"/> over <see cref="ApiRoutes.Users"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IUsersService>]
public sealed class UsersApiClient : IUsersService
{
    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public UsersApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public IAsyncEnumerable<UserSummary> GetUsersAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<UserSummary>(ApiRoutes.Users, cancellationToken);

    /// <inheritdoc/>
    public async Task<ResetLink?> CreateResetLinkAsync(Guid userId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Users}/{userId}/reset-link", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        ThrowIfMissing(response, userId);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<ResetLink>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> LockAsync(Guid userId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Users}/{userId}/lock", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        ThrowIfMissing(response, userId);
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc/>
    public async Task UnlockAsync(Guid userId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Users}/{userId}/unlock", null, cancellationToken);
        ThrowIfMissing(response, userId);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Throws on a 404, as the interface documents.
    /// </summary>
    /// <param name="response">The API's answer.</param>
    /// <param name="userId">The user asked for.</param>
    /// <exception cref="InvalidOperationException">There's no such user.</exception>
    private static void ThrowIfMissing(HttpResponseMessage response, Guid userId)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"There's no user {userId}");
        }
    }
}
