using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IPayPeriodsService"/> over <see cref="ApiRoutes.PayPeriods"/>. The API's 404s come back as the
/// <see cref="InvalidOperationException"/> the interface documents.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IPayPeriodsService>]
public sealed class PayPeriodsApiClient : IPayPeriodsService
{
    private readonly HttpClient _http;

    public PayPeriodsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public async Task<YourPay> GetYourPayAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync(ApiRoutes.PayPeriods, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadRequiredJsonAsync<YourPay>(cancellationToken);
    }

    public IAsyncEnumerable<Income> GetIncomesAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Income>($"{ApiRoutes.PayPeriods}/incomes", cancellationToken);

    public async Task<YourPay> ChooseAsync(ChooseIncome income, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.PayPeriods}/pay", income, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<YourPay>(cancellationToken);
    }

    public Task<YourPay> TurnOffAsync(CancellationToken cancellationToken) => PutAsync("off", cancellationToken);

    public Task<YourPay> TurnOnAsync(CancellationToken cancellationToken) => PutAsync("on", cancellationToken);

    private async Task<YourPay> PutAsync(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsync($"{ApiRoutes.PayPeriods}/{path}", null, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<YourPay>(cancellationToken);
    }
}
