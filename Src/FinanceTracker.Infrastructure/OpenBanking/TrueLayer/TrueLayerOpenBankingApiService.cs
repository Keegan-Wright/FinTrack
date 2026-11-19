using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Telemetry;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer;

/// <inheritdoc cref="IOpenBankingApiService"/>
[InjectionCategory(InjectionCategoryType.External, ServiceDestinationType.Server)]
[Scoped<IOpenBankingApiService>]
public class TrueLayerOpenBankingApiService : IOpenBankingApiService
{
    /// <summary>
    /// Named <see cref="HttpClient"/> registered for TrueLayer's auth requests and provider logos.
    /// </summary>
    public const string StandardHttpClientName = "OpenBankingClient";

    /// <summary>
    /// Named <see cref="HttpClient"/> registered for TrueLayer's data API. Throttled, so a
    /// running sync doesn't hold up sign-ins or logos on <see cref="StandardHttpClientName"/>.
    /// </summary>
    public const string DataHttpClientName = "OpenBankingDataClient";

    private readonly TrueLayerOpenBankingConfiguration _trueLayerOpenBankingConfiguration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TrueLayerOpenBankingApiService> _logger;

    /// <param name="trueLayerOpenBankingConfiguration">TrueLayer's settings.</param>
    /// <param name="httpClientFactory">Makes the API's client.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">Its logger.</param>
    public TrueLayerOpenBankingApiService(TrueLayerOpenBankingConfiguration trueLayerOpenBankingConfiguration,
        IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger<TrueLayerOpenBankingApiService> logger)
    {
        _trueLayerOpenBankingConfiguration = trueLayerOpenBankingConfiguration;
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }


    /// <inheritdoc/>
    public async Task<ExternalOpenBankingAccessResponse> ExchangeCodeForAccessTokenAsync(string vendorAccessCode,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildStandardHttpClient();

        List<KeyValuePair<string, string>> formData =
        [
            new("grant_type", "authorization_code"),
            new("client_id", _trueLayerOpenBankingConfiguration.ClientId),
            new("client_secret",
                _trueLayerOpenBankingConfiguration.ClientSecret.ToString()),

            new("redirect_uri",
                _trueLayerOpenBankingConfiguration.AuthRedirectUrl.ToString()),
            new("code", vendorAccessCode)
        ];

        HttpContent content = new FormUrlEncodedContent(formData);

        HttpResponseMessage response = await httpClient.PostAsync("connect/token", content, cancellationToken);

        // A rejected or expired code comes back as an error body, fail here rather than on its missing tokens later
        EnsureSuccess(response, "access code exchange");

        return await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccessResponse>(cancellationToken)
               ?? throw new InvalidOperationException("The provider returned no tokens for the access code.");
    }

    /// <inheritdoc/>
    public async Task<ExternalOpenBankingAccessResponse> GetAccessTokenByRefreshTokenAsync(string refreshToken,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildStandardHttpClient();

        List<KeyValuePair<string, string>> formData =
        [
            new("grant_type", "refresh_token"),
            new("client_id", _trueLayerOpenBankingConfiguration.ClientId),
            new("client_secret",
                _trueLayerOpenBankingConfiguration.ClientSecret.ToString()),
            new("refresh_token", refreshToken)
        ];

        HttpContent content = new FormUrlEncodedContent(formData);

        HttpResponseMessage response = await httpClient.PostAsync("connect/token", content, cancellationToken);

        // A rejected or expired code comes back as an error body, fail here rather than on its missing tokens later
        EnsureSuccess(response, "token refresh");

        return await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccessResponse>(cancellationToken)
               ?? throw new InvalidOperationException("The provider returned no tokens for the access code.");
    }

    /// <inheritdoc/>
    public async Task<string> GetReauthenticationUrlAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildStandardHttpClient();

        var request = new ExternalOpenBankingReauthenticationRequest
        {
            RefreshToken = refreshToken,
            RedirectUri = _trueLayerOpenBankingConfiguration.AuthRedirectUrl.ToString()
        };

        HttpResponseMessage response = await httpClient.PostAsJsonAsync("v1/reauthuri", request, cancellationToken);
        EnsureSuccess(response, "reauthentication link");

        ExternalOpenBankingReauthenticationResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingReauthenticationResponse>(cancellationToken);

        if (responseBody is null || !responseBody.Success || string.IsNullOrWhiteSpace(responseBody.Result))
        {
            throw new HttpRequestException("TrueLayer didn't return a reauthentication link.");
        }

        return responseBody.Result;
    }

    /// <inheritdoc/>
    public async Task<ExternalOpenBankingListAllAccountsResponse> GetAllAccountsAsync(string authToken,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildDataHttpClient(authToken);

        HttpResponseMessage response = await httpClient.GetAsync("v1/accounts", cancellationToken);
        if (!Succeeded(response, "accounts"))
        {
            return new ExternalOpenBankingListAllAccountsResponse();
        }

        ExternalOpenBankingListAllAccountsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingListAllAccountsResponse>(cancellationToken);

        return responseBody!;
    }

    /// <inheritdoc/>
    public Task<ExternalOpenBankingGetAccountBalanceResponse> GetAccountBalanceAsync(string accountId,
        string authToken, CancellationToken cancellationToken) =>
        GetBalanceAsync($"v1/accounts/{accountId}", "account balance", authToken, cancellationToken);

    /// <inheritdoc/>
    public Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountTransactionsAsync(string accountId,
        string authToken, DateTime? transactionsStartingDate, CancellationToken cancellationToken) =>
        GetTransactionsAsync($"v1/accounts/{accountId}/transactions", "transactions", authToken,
            transactionsStartingDate, cancellationToken);

    /// <inheritdoc/>
    public Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountPendingTransactionsAsync(
        string accountId, string authToken, DateTime? transactionsStartingDate,
        CancellationToken cancellationToken) =>
        GetTransactionsAsync($"v1/accounts/{accountId}/transactions/pending", "pending transactions", authToken,
            transactionsStartingDate, cancellationToken);

    /// <inheritdoc/>
    public async Task<ExternalOpenBankingListAllCardsResponse> GetAllCardsAsync(string authToken,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildDataHttpClient(authToken);

        HttpResponseMessage response = await httpClient.GetAsync("v1/cards", cancellationToken);
        if (!Succeeded(response, "cards"))
        {
            return new ExternalOpenBankingListAllCardsResponse();
        }

        ExternalOpenBankingListAllCardsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingListAllCardsResponse>(cancellationToken);

        return responseBody!;
    }

    /// <inheritdoc/>
    public Task<ExternalOpenBankingGetAccountBalanceResponse> GetCardBalanceAsync(string cardId, string authToken,
        CancellationToken cancellationToken) =>
        GetBalanceAsync($"v1/cards/{cardId}", "card balance", authToken, cancellationToken);

    /// <inheritdoc/>
    public Task<ExternalOpenBankingAccountTransactionsResponse> GetCardTransactionsAsync(string cardId,
        string authToken, DateTime? transactionsStartingDate, CancellationToken cancellationToken) =>
        GetTransactionsAsync($"v1/cards/{cardId}/transactions", "card transactions", authToken,
            transactionsStartingDate, cancellationToken);

    /// <inheritdoc/>
    public Task<ExternalOpenBankingAccountTransactionsResponse> GetCardPendingTransactionsAsync(string cardId,
        string authToken, DateTime? transactionsStartingDate, CancellationToken cancellationToken) =>
        GetTransactionsAsync($"v1/cards/{cardId}/transactions/pending", "card pending transactions", authToken,
            transactionsStartingDate, cancellationToken);

    /// <summary>
    /// An account's or card's balance, from <paramref name="resource"/>, e.g. <c>v1/cards/{id}</c>.
    /// </summary>
    /// <param name="resource">The account's or card's path.</param>
    /// <param name="requested">What's asked for, for the log.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<ExternalOpenBankingGetAccountBalanceResponse> GetBalanceAsync(string resource,
        string requested, string authToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildDataHttpClient(authToken);
        HttpResponseMessage response = await httpClient.GetAsync($"{resource}/balance", cancellationToken);

        // Throws rather than returning empty, so a refused request isn't recorded as a balance sync
        EnsureSuccess(response, requested);

        ExternalOpenBankingGetAccountBalanceResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingGetAccountBalanceResponse>(cancellationToken);

        return responseBody!;
    }

    /// <summary>
    /// Booked or pending transactions at <paramref name="path"/>, from <paramref name="transactionsStartingDate"/> to
    /// today when given, otherwise everything the provider has.
    /// </summary>
    /// <param name="path">The transactions' path.</param>
    /// <param name="requested">What's asked for, for the log.</param>
    /// <param name="authToken">The connection's access token.</param>
    /// <param name="transactionsStartingDate">The earliest to get, null for everything.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<ExternalOpenBankingAccountTransactionsResponse> GetTransactionsAsync(string path,
        string requested, string authToken, DateTime? transactionsStartingDate, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildDataHttpClient(authToken);

        StringBuilder urlBuilder = new();
        urlBuilder.Append(path);

        if (transactionsStartingDate.HasValue)
        {
            urlBuilder.Append(CultureInfo.CurrentCulture,
                $"?from={transactionsStartingDate:yyyy-MM-dd}&to={_timeProvider.GetLocalNow():yyyy-MM-dd}");
        }

        // Throws rather than returning empty, so a refused request isn't recorded as a transaction sync, and an empty
        // pending list doesn't remove every stored pending transaction
        HttpResponseMessage response = await httpClient.GetAsync(urlBuilder.ToString(), cancellationToken);
        EnsureSuccess(response, requested);

        ExternalOpenBankingAccountTransactionsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountTransactionsResponse>(cancellationToken);

        return responseBody!;
    }

    /// <inheritdoc/>
    public async Task<ExternalOpenBankingAccountStandingOrdersResponse> GetAccountStandingOrdersAsync(string accountId,
        string authToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildDataHttpClient(authToken);

        HttpResponseMessage response =
            await httpClient.GetAsync($"v1/accounts/{accountId}/standing_orders", cancellationToken);

        // Throws rather than returning empty, an empty list removes every stored standing order
        EnsureSuccess(response, "standing orders");

        ExternalOpenBankingAccountStandingOrdersResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountStandingOrdersResponse>(
                cancellationToken);

        return responseBody!;
    }

    /// <inheritdoc/>
    public async Task<ExternalOpenBankingAccountDirectDebitsResponse> GetAccountDirectDebitsAsync(string accountId,
        string authToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildDataHttpClient(authToken);

        HttpResponseMessage response =
            await httpClient.GetAsync($"v1/accounts/{accountId}/direct_debits", cancellationToken);

        // Throws rather than returning empty, an empty list removes every stored direct debit
        EnsureSuccess(response, "direct debits");

        ExternalOpenBankingAccountDirectDebitsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountDirectDebitsResponse>(cancellationToken);

        return responseBody!;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ExternalOpenBankingProvider> GetAvailableProvidersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildStandardHttpClient();

        HttpResponseMessage response =
            await httpClient.GetAsync($"api/providers?client_id={_trueLayerOpenBankingConfiguration.ClientId}",
                cancellationToken);

        // The list is cached, a failure has to throw rather than read as no providers
        EnsureSuccess(response, "providers");

        await foreach (ExternalOpenBankingProvider? provider in response.Content
                           .ReadFromJsonAsAsyncEnumerable<ExternalOpenBankingProvider>(cancellationToken))
        {
            if (provider is null)
                continue;

            yield return provider;
        }
    }

    /// <inheritdoc/>
    public string BuildAuthUrl(IEnumerable<string> providerIds, IEnumerable<string> scopes)
    {
        StringBuilder sb = new();

        sb.Append(_trueLayerOpenBankingConfiguration.BaseAuthUrl);
        sb.Append(CultureInfo.CurrentCulture,
            $"?response_type=code&client_id={_trueLayerOpenBankingConfiguration.ClientId}");
        sb.Append('&');
        sb.Append(CultureInfo.CurrentCulture, $"scope={Uri.EscapeDataString(string.Join(" ", scopes))}");
        sb.Append(Uri.EscapeDataString(" "));


        sb.Append(CultureInfo.CurrentCulture, $"&redirect_uri={_trueLayerOpenBankingConfiguration.AuthRedirectUrl}");
        sb.Append(CultureInfo.CurrentCulture, $"&providers={Uri.EscapeDataString(string.Join(" ", providerIds))}");

        sb.Append(Uri.EscapeDataString(" "));

        sb.Append("uk-oauth-all");

        return sb.ToString();
    }

    /// <inheritdoc/>
    public async Task<ExternalOpenBankingAccountConnectionResponse> GetProviderInformation(string accessToken,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildDataHttpClient(accessToken);

        HttpResponseMessage response = await httpClient.GetAsync("v1/me", cancellationToken);
        if (!Succeeded(response, "connection information"))
        {
            return new ExternalOpenBankingAccountConnectionResponse();
        }

        ExternalOpenBankingAccountConnectionResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountConnectionResponse>(cancellationToken);

        return responseBody!;
    }

    /// <inheritdoc/>
    public async Task<byte[]> GetProviderLogoAsync(string logoUri, CancellationToken cancellationToken)
    {
        // Logos are public assets on their own host, no base address or auth headers
        using HttpClient httpClient = _httpClientFactory.CreateClient(StandardHttpClientName);

        return await httpClient.GetByteArrayAsync(logoUri, cancellationToken);
    }

    /// <summary>
    /// A client for TrueLayer's auth API.
    /// </summary>
    private Task<HttpClient> BuildStandardHttpClient() =>
        BuildHttpClient(StandardHttpClientName, _trueLayerOpenBankingConfiguration.BaseAuthUrl, null);

    /// <summary>
    /// A client for TrueLayer's data API, signed in with <paramref name="authToken"/>.
    /// </summary>
    /// <param name="authToken">The connection's access token.</param>
    private Task<HttpClient> BuildDataHttpClient(string authToken) =>
        BuildHttpClient(DataHttpClientName, _trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

    /// <summary>
    /// A named client for <paramref name="baseUrl"/>, with the user's address header TrueLayer asks for.
    /// </summary>
    /// <param name="clientName">The factory's client.</param>
    /// <param name="baseUrl">The API's address.</param>
    /// <param name="authHeader">The access token, null for none.</param>
    private async Task<HttpClient> BuildHttpClient(string clientName, Uri baseUrl, string? authHeader)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient(clientName);
        httpClient.BaseAddress = baseUrl;

        if (!string.IsNullOrEmpty(authHeader))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authHeader);
        }

        httpClient.DefaultRequestHeaders.Add("x-PSU-IP", _trueLayerOpenBankingConfiguration.PublicIpAddress);

        httpClient.Timeout = TimeSpan.FromMinutes(5);

        return httpClient;
    }

    /// <summary>
    /// Whether TrueLayer accepted the request. A refusal is logged with TrueLayer's trace id.
    /// </summary>
    /// <param name="response">The provider's answer.</param>
    /// <param name="requested">What was asked for, for the log.</param>
    private bool Succeeded(HttpResponseMessage response, string requested)
    {
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        string traceId = TraceId(response);
        _logger.LogTrueLayerRequestRefused(requested, (int)response.StatusCode, response.ReasonPhrase, traceId);

        // On the span that asked, such as a sync's fetch, so the trace leads to what TrueLayer's support can look up
        Activity.Current?.SetTag(TelemetryTags.TrueLayerTraceId, traceId);
        return false;
    }

    /// <summary>
    /// Throws when TrueLayer refused the request, after logging it. The message carries the status and TrueLayer's
    /// trace id.
    /// </summary>
    /// <param name="response">The provider's answer.</param>
    /// <param name="requested">What was asked for, for the log.</param>
    private void EnsureSuccess(HttpResponseMessage response, string requested)
    {
        if (Succeeded(response, requested))
        {
            return;
        }

        throw new HttpRequestException(
            $"TrueLayer refused the {requested} request with {(int)response.StatusCode} {response.ReasonPhrase}, tl-trace-id {TraceId(response)}",
            null, response.StatusCode);
    }

    /// <summary>
    /// TrueLayer's trace id on the answer, for the log, <c>none</c> without one.
    /// </summary>
    /// <param name="response">The provider's answer.</param>
    private static string TraceId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("tl-trace-id", out IEnumerable<string>? traceIds))
        {
            return string.Join(",", traceIds);
        }

        return "none";
    }
}
