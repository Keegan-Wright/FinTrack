using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// The open banking provider's API, TrueLayer's. Its auth API for tokens and connecting, its data API for what's
/// synced.
/// </summary>
public interface IOpenBankingApiService
{
    /// <summary>
    /// Exchanges the code a bank gave back, once the user consented, for tokens.
    /// </summary>
    /// <param name="vendorAccessCode">The code the bank gave back.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingAccessResponse> ExchangeCodeForAccessTokenAsync(string vendorAccessCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a new access token with the refresh token.
    /// </summary>
    /// <param name="refreshToken">The connection's latest refresh token.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingAccessResponse> GetAccessTokenByRefreshTokenAsync(string refreshToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// A link that signs the user in to their bank again to renew the consent behind
    /// <paramref name="refreshToken"/>, keeping the same connection. They come back with an access code, as when
    /// connecting.
    /// </summary>
    /// <param name="refreshToken">The connection's latest refresh token.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <exception cref="HttpRequestException">The provider refused.</exception>
    Task<string> GetReauthenticationUrlAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// The connection's accounts.
    /// </summary>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingListAllAccountsResponse> GetAllAccountsAsync(string authToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// An account's balance.
    /// </summary>
    /// <param name="accountId">TrueLayer's id for the account.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingGetAccountBalanceResponse> GetAccountBalanceAsync(string accountId, string authToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// An account's settled transactions.
    /// </summary>
    /// <param name="accountId">TrueLayer's id for the account.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="transactionsStartingDate">The earliest to get, null for as far back as the bank gives.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountTransactionsAsync(string accountId, string authToken,
        DateTime? transactionsStartingDate, CancellationToken cancellationToken);

    /// <summary>
    /// An account's pending transactions.
    /// </summary>
    /// <param name="accountId">TrueLayer's id for the account.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="transactionsStartingDate">The earliest to get, null for as far back as the bank gives.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountPendingTransactionsAsync(string accountId,
        string authToken, DateTime? transactionsStartingDate, CancellationToken cancellationToken);

    /// <summary>
    /// The connection's credit and charge cards. Needs the <c>cards</c> scope.
    /// </summary>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingListAllCardsResponse> GetAllCardsAsync(string authToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// A card's balance as the provider reports it, including its credit limit.
    /// </summary>
    /// <param name="cardId">TrueLayer's id for the card.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingGetAccountBalanceResponse> GetCardBalanceAsync(string cardId, string authToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// A card's settled transactions.
    /// </summary>
    /// <param name="cardId">TrueLayer's id for the card.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="transactionsStartingDate">The earliest to get, null for as far back as the bank gives.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingAccountTransactionsResponse> GetCardTransactionsAsync(string cardId, string authToken,
        DateTime? transactionsStartingDate, CancellationToken cancellationToken);

    /// <summary>
    /// A card's pending transactions.
    /// </summary>
    /// <param name="cardId">TrueLayer's id for the card.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="transactionsStartingDate">The earliest to get, null for as far back as the bank gives.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingAccountTransactionsResponse> GetCardPendingTransactionsAsync(string cardId,
        string authToken, DateTime? transactionsStartingDate, CancellationToken cancellationToken);

    /// <summary>
    /// An account's standing orders. Only released soon after the user authenticates.
    /// </summary>
    /// <param name="accountId">TrueLayer's id for the account.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingAccountStandingOrdersResponse> GetAccountStandingOrdersAsync(string accountId,
        string authToken, CancellationToken cancellationToken);

    /// <summary>
    /// An account's direct debits. Only released soon after the user authenticates.
    /// </summary>
    /// <param name="accountId">TrueLayer's id for the account.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingAccountDirectDebitsResponse> GetAccountDirectDebitsAsync(string accountId, string authToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// The banks that can be connected.
    /// </summary>
    /// <param name="cancellationToken">Stops the call.</param>
    IAsyncEnumerable<ExternalOpenBankingProvider> GetAvailableProvidersAsync(CancellationToken cancellationToken);
    /// <summary>
    /// The link that takes the user to connect their bank.
    /// </summary>
    /// <param name="providerIds">The banks to offer.</param>
    /// <param name="scopes">What to ask the user to share.</param>
    string BuildAuthUrl(IEnumerable<string> providerIds, IEnumerable<string> scopes);

    /// <summary>
    /// The connection's details, such as its bank and when consent ends.
    /// </summary>
    /// <param name="accessToken">The connection's access token.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<ExternalOpenBankingAccountConnectionResponse> GetProviderInformation(string accessToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// A bank's logo.
    /// </summary>
    /// <param name="logoUri">Where the logo is.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    Task<byte[]> GetProviderLogoAsync(string logoUri, CancellationToken cancellationToken);
}
