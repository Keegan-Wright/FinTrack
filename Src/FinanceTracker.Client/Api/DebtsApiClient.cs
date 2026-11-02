using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IDebtsService"/> over <see cref="ApiRoutes.Debts"/>. The API's 404s come back as the
/// <see cref="InvalidOperationException"/> the interface documents, and its 400 for a forecast payment that isn't more
/// than 0 as the <see cref="ArgumentOutOfRangeException"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Client)]
[Scoped<IDebtsService>]
public sealed class DebtsApiClient : IDebtsService
{
    private readonly HttpClient _http;

    public DebtsApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<Debt> GetDebtsAsync(CancellationToken cancellationToken) =>
        _http.GetStreamAsync<Debt>(ApiRoutes.Debts, cancellationToken);

    public async Task<DebtDetail> GetDebtAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.GetAsync($"{ApiRoutes.Debts}/{id}", cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<DebtDetail>(cancellationToken);
    }

    public async Task<Debt> AddDebtAsync(SaveDebt debt, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(ApiRoutes.Debts, debt, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Debt>(cancellationToken);
    }

    public async Task<Debt> UpdateDebtAsync(Guid id, SaveDebt debt, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.Debts}/{id}", debt, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Debt>(cancellationToken);
    }

    public async Task DeleteDebtAsync(Guid id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _http.DeleteAsync($"{ApiRoutes.Debts}/{id}", cancellationToken);
        response.EnsureFound();
    }

    public async Task<DebtForecast> ForecastAsync(Guid id, decimal monthlyPayment,
        CancellationToken cancellationToken)
    {
        string payment = monthlyPayment.ToString(CultureInfo.InvariantCulture);
        using HttpResponseMessage response = await _http.GetAsync(
            $"{ApiRoutes.Debts}/{id}/forecast?monthlyPayment={payment}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyPayment), monthlyPayment,
                "The monthly payment must be more than 0.");
        }

        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<DebtForecast>(cancellationToken);
    }

    public async Task<Transaction> PayDebtAsync(Guid transactionId, PayDebt payment,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _http.PutAsJsonAsync($"{ApiRoutes.Debts}/payments/{transactionId}", payment, cancellationToken);
        response.EnsureFound();
        return await response.ReadRequiredJsonAsync<Transaction>(cancellationToken);
    }
}
