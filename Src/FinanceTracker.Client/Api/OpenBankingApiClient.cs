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

    /// <param name="httpClientFactory">Makes the API's client.</param>
    public OpenBankingApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    /// <inheritdoc/>
    public IAsyncEnumerable<OpenBankingProvider> GetOpenBankingProvidersForClientAsync(
        CancellationToken cancellationToken) =>
        _http.GetStreamAsync<OpenBankingProvider>($"{ApiRoutes.OpenBanking}/providers", cancellationToken);

    /// <inheritdoc/>
    public Task<string> BuildAuthUrlAsync(ProviderSetupUrl setupProvider, CancellationToken cancellationToken) =>
        _http.PostReadAsync<ProviderSetupUrl, string>($"{ApiRoutes.OpenBanking}/auth-url", setupProvider,
            cancellationToken);

    /// <inheritdoc/>
    public Task<bool> AddVendorViaAccessCodeAsync(AddVendor addVendor, CancellationToken cancellationToken) =>
        _http.PostReadAsync<AddVendor, bool>($"{ApiRoutes.OpenBanking}/connections", addVendor, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<BankConnectionSummary> GetConnectionsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<BankConnectionSummary>($"{ApiRoutes.OpenBanking}/connections", cancellationToken);

    /// <inheritdoc/>
    public Task<string> GetRenewalUrlAsync(Guid connectionId, CancellationToken cancellationToken) =>
        _http.PostReadAsync<object?, string>($"{ApiRoutes.OpenBanking}/connections/{connectionId}/renewal-url", null,
            cancellationToken);
}
