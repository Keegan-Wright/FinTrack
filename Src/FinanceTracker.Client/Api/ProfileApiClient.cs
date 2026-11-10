using System.Net;
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
    private const string Email = $"{ApiRoutes.Profile}/email";

    private readonly HttpClient _http;

    public ProfileApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public async Task<EmailStatus> GetEmailStatusAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(Email, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<EmailStatus>(cancellationToken);
    }

    public async Task<bool> SendEmailConfirmationAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsync($"{Email}/confirmation", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
