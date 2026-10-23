using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IReportService"/> over <see cref="ApiRoutes.Reports"/>.
/// </summary>
public sealed class ReportApiClient : IReportService
{
    private readonly HttpClient _http;

    public ReportApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<SpentInTimePeriodReport> GetSpentInTimePeriodReportAsync(FilterTransactions request,
        CancellationToken cancellationToken) =>
        _http.PostStreamAsync<FilterTransactions, SpentInTimePeriodReport>(
            $"{ApiRoutes.Reports}/spent-in-time-period", request, cancellationToken);

    public IAsyncEnumerable<SpentInCategoryReport> GetCategoryBreakdownReportAsync(FilterTransactions request,
        CancellationToken cancellationToken) =>
        _http.PostStreamAsync<FilterTransactions, SpentInCategoryReport>($"{ApiRoutes.Reports}/category-breakdown",
            request, cancellationToken);

    public IAsyncEnumerable<SpentInAccountReport> GetAccountBreakdownReportAsync(FilterTransactions request,
        CancellationToken cancellationToken) =>
        _http.PostStreamAsync<FilterTransactions, SpentInAccountReport>($"{ApiRoutes.Reports}/account-breakdown",
            request, cancellationToken);
}
