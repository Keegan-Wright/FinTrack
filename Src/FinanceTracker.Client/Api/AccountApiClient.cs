using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IAccountService"/> over <see cref="ApiRoutes.Accounts"/>.
/// </summary>
public sealed class AccountApiClient : IAccountService
{
    private readonly HttpClient _http;

    public AccountApiClient(IHttpClientFactory httpClientFactory) =>
        _http = httpClientFactory.CreateClient(ApiClientRegistration.HttpClientName);

    public IAsyncEnumerable<AccountAndTransactions> GetAccountsAndMostRecentTransactionsAsync(
        int transactionsToReturn, CancellationToken cancellationToken) =>
        _http.GetStreamAsync<AccountAndTransactions>($"{ApiRoutes.Accounts}?transactions={transactionsToReturn}",
            cancellationToken);
}
