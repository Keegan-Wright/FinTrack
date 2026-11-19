using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IPasskeyService"/> over the API. A 409 answers null, the account has no password.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IPasskeyService>]
public sealed class PasskeyApiClient : IPasskeyService
{
    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public PasskeyApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PasskeySummary>?> GetPasskeysAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(ApiRoutes.Passkeys, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<List<PasskeySummary>>(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PasskeyChange?> StartAddingAsync(ConfirmPassword request, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, $"{ApiRoutes.Passkeys}/options", request, cancellationToken);

    /// <inheritdoc/>
    public Task<PasskeyChange?> AddAsync(AddPasskey request, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, ApiRoutes.Passkeys, request, cancellationToken);

    /// <inheritdoc/>
    public Task<PasskeyChange?> RenameAsync(string id, RenamePasskey request, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, $"{ApiRoutes.Passkeys}/{Uri.EscapeDataString(id)}", request, cancellationToken);

    /// <inheritdoc/>
    public Task<PasskeyChange?> RemoveAsync(string id, ConfirmPassword request,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, $"{ApiRoutes.Passkeys}/{Uri.EscapeDataString(id)}/remove", request,
            cancellationToken);

    /// <summary>
    /// Sends <paramref name="request"/> and reads back how the change went. Null on a 409.
    /// </summary>
    /// <typeparam name="T">The request's type.</typeparam>
    /// <param name="method">How it's sent.</param>
    /// <param name="uri">The API path.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<PasskeyChange?> SendAsync<T>(HttpMethod method, string uri, T request,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage message = new(method, uri) { Content = JsonContent.Create(request) };
        using HttpResponseMessage response = await _http.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<PasskeyChange>(cancellationToken);
    }
}
