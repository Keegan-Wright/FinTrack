using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Diagnostics;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IClientErrorsService"/> over <see cref="ApiRoutes.ClientErrors"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IClientErrorsService>]
public sealed class ClientErrorsApiClient : IClientErrorsService
{
    private readonly HttpClient _http;

    public ClientErrorsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public async Task ReportAsync(ClientError error, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PostAsJsonAsync(ApiRoutes.ClientErrors, error, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
