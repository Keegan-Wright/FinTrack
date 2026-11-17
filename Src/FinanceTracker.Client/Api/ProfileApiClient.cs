using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IProfileService"/> over <see cref="ApiRoutes.Profile"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IProfileService>]
public sealed class ProfileApiClient : IProfileService
{
    private readonly HttpClient _http;

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public ProfileApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public async Task<ProfileDetails> GetProfileAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(ApiRoutes.Profile, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<ProfileDetails>(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SendEmailConfirmationAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsync($"{ApiRoutes.Profile}/email/confirmation", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc/>
    public Task<ProfileChange?> ChangePasswordAsync(ChangePassword change, CancellationToken cancellationToken) =>
        PutAsync($"{ApiRoutes.Profile}/password", change, cancellationToken);

    /// <inheritdoc/>
    public Task<ProfileChange?> ChangeEmailAsync(ChangeEmail change, CancellationToken cancellationToken) =>
        PutAsync($"{ApiRoutes.Profile}/email", change, cancellationToken);

    /// <summary>
    /// Puts <paramref name="change"/> and reads back what happened. Null on a 409, the account changed meanwhile.
    /// </summary>
    /// <typeparam name="T">The change's type.</typeparam>
    /// <param name="uri">The API path.</param>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    private async Task<ProfileChange?> PutAsync<T>(string uri, T change, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PutAsJsonAsync(uri, change, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<ProfileChange>(cancellationToken);
    }
}
