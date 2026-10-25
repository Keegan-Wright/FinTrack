using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.OpenBanking;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IOpenBankingService"/> over <see cref="ApiRoutes.OpenBanking"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IOpenBankingService>]
public sealed class OpenBankingApiClient : IOpenBankingService
{
    private readonly HttpClient _http;

    public OpenBankingApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<OpenBankingProvider> GetOpenBankingProvidersForClientAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<OpenBankingProvider>($"{ApiRoutes.OpenBanking}/providers", cancellationToken);

    public Task<string> BuildAuthUrlAsync(ProviderSetupUrl setupProvider, CancellationToken cancellationToken) =>
        _http.PostReadAsync<ProviderSetupUrl, string>($"{ApiRoutes.OpenBanking}/auth-url", setupProvider,
            cancellationToken);

    public Task<bool> AddVendorViaAccessCodeAsync(AddVendor addVendor, CancellationToken cancellationToken) =>
        _http.PostReadAsync<AddVendor, bool>($"{ApiRoutes.OpenBanking}/connections", addVendor, cancellationToken);
}
