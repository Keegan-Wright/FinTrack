using FinanceTracker.Models.External;

namespace FinanceTracker.Services.External.OpenBanking;

public interface IOpenBankingApiService
{
    Task<ExternalOpenBankingAccessResponse> ExchangeCodeForAccessTokenAsync(string vendorAccessCode, CancellationToken cancellationToken);
    Task<ExternalOpenBankingAccessResponse> GetAccessTokenByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task<ExternalOpenBankingListAllAccountsResponse> GetAllAccountsAsync(string authToken, CancellationToken cancellationToken);
    Task<ExternalOpenBankingGetAccountBalanceResponse> GetAccountBalanceAsync(string accountId, string authToken, CancellationToken cancellationToken);
    Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountTransactionsAsync(string accountId, string authToken, DateTime? transactionsStartingDate, CancellationToken cancellationToken);
    Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountPendingTransactionsAsync(string accountId, string authToken, DateTime? transactionsStartingDate, CancellationToken cancellationToken);
    Task<ExternalOpenBankingAccountStandingOrdersResponse> GetAccountStandingOrdersAsync(string accountId, string authToken, CancellationToken cancellationToken);
    Task<ExternalOpenBankingAccountDirectDebitsResponse> GetAccountDirectDebitsAsync(string accountId, string authToken, CancellationToken cancellationToken);
    IAsyncEnumerable<ExternalOpenBankingProvider> GetAvailableProvidersAsync(CancellationToken cancellationToken);
    string BuildAuthUrl(IEnumerable<string> providerIds, IEnumerable<string> scopes);
    Task<ExternalOpenBankingAccountConnectionResponse> GetProviderInformation(string accessToken, CancellationToken cancellationToken);

}