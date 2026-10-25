using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Client.Api;

/// <summary>
/// <see cref="IAccountService"/> over <see cref="ApiRoutes.Accounts"/>.
/// </summary>
[InjectionCategory(InjectionCategoryType.ApiClient)]
[Scoped<IAccountService>]
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
