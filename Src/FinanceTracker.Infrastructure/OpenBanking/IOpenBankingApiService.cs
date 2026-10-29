using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Infrastructure.OpenBanking;

public interface IOpenBankingApiService
{
    Task<ExternalOpenBankingAccessResponse> ExchangeCodeForAccessTokenAsync(string vendorAccessCode,
        CancellationToken cancellationToken);

    Task<ExternalOpenBankingAccessResponse> GetAccessTokenByRefreshTokenAsync(string refreshToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// A link that signs the user in to their bank again to renew the consent behind
    /// <paramref name="refreshToken"/>, keeping the same connection. They come back with an access code, as when
    /// connecting.
    /// </summary>
    /// <exception cref="HttpRequestException">The provider refused.</exception>
    Task<string> GetReauthenticationUrlAsync(string refreshToken, CancellationToken cancellationToken);

    Task<ExternalOpenBankingListAllAccountsResponse> GetAllAccountsAsync(string authToken,
        CancellationToken cancellationToken);

    Task<ExternalOpenBankingGetAccountBalanceResponse> GetAccountBalanceAsync(string accountId, string authToken,
        CancellationToken cancellationToken);

    Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountTransactionsAsync(string accountId, string authToken,
        DateTime? transactionsStartingDate, CancellationToken cancellationToken);

    Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountPendingTransactionsAsync(string accountId,
        string authToken, DateTime? transactionsStartingDate, CancellationToken cancellationToken);

    /// <summary>
    /// The connection's credit and charge cards. Needs the <c>cards</c> scope.
    /// </summary>
    Task<ExternalOpenBankingListAllCardsResponse> GetAllCardsAsync(string authToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// A card's balance as the provider reports it, including its credit limit.
    /// </summary>
    Task<ExternalOpenBankingGetAccountBalanceResponse> GetCardBalanceAsync(string cardId, string authToken,
        CancellationToken cancellationToken);

    Task<ExternalOpenBankingAccountTransactionsResponse> GetCardTransactionsAsync(string cardId, string authToken,
        DateTime? transactionsStartingDate, CancellationToken cancellationToken);

    Task<ExternalOpenBankingAccountTransactionsResponse> GetCardPendingTransactionsAsync(string cardId,
        string authToken, DateTime? transactionsStartingDate, CancellationToken cancellationToken);

    Task<ExternalOpenBankingAccountStandingOrdersResponse> GetAccountStandingOrdersAsync(string accountId,
        string authToken, CancellationToken cancellationToken);

    Task<ExternalOpenBankingAccountDirectDebitsResponse> GetAccountDirectDebitsAsync(string accountId, string authToken,
        CancellationToken cancellationToken);

    IAsyncEnumerable<ExternalOpenBankingProvider> GetAvailableProvidersAsync(CancellationToken cancellationToken);
    string BuildAuthUrl(IEnumerable<string> providerIds, IEnumerable<string> scopes);

    Task<ExternalOpenBankingAccountConnectionResponse> GetProviderInformation(string accessToken,
        CancellationToken cancellationToken);

    Task<byte[]> GetProviderLogoAsync(string logoUri, CancellationToken cancellationToken);
}
