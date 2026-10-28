using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.OpenBanking.TrueLayer;

[InjectionCategory(InjectionCategoryType.External, ServiceDestinationType.Server)]
[Scoped<IOpenBankingApiService>]
public class TrueLayerOpenBankingApiService : IOpenBankingApiService
{
    /// <summary>
    /// Named <see cref="HttpClient"/> registered for TrueLayer and provider logo requests.
    /// </summary>
    public const string HttpClientName = "OpenBankingClient";

    private readonly TrueLayerOpenBankingConfiguration _trueLayerOpenBankingConfiguration;
    private readonly IHttpClientFactory _clientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TrueLayerOpenBankingApiService> _logger;

    public TrueLayerOpenBankingApiService(TrueLayerOpenBankingConfiguration trueLayerOpenBankingConfiguration,
        IHttpClientFactory clientFactory, TimeProvider timeProvider, ILogger<TrueLayerOpenBankingApiService> logger)
    {
        _trueLayerOpenBankingConfiguration = trueLayerOpenBankingConfiguration;
        _clientFactory = clientFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }


    public async Task<ExternalOpenBankingAccessResponse> ExchangeCodeForAccessTokenAsync(string vendorAccessCode,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseAuthUrl);

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

    public async Task<ExternalOpenBankingAccessResponse> GetAccessTokenByRefreshTokenAsync(string refreshToken,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseAuthUrl);

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

    public async Task<string> GetReauthenticationUrlAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseAuthUrl);

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

    public async Task<ExternalOpenBankingListAllAccountsResponse> GetAllAccountsAsync(string authToken,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        HttpResponseMessage response = await httpClient.GetAsync("v1/accounts", cancellationToken);
        if (!Succeeded(response, "accounts"))
        {
            return new ExternalOpenBankingListAllAccountsResponse();
        }

        ExternalOpenBankingListAllAccountsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingListAllAccountsResponse>(cancellationToken);

        return responseBody!;
    }

    public async Task<ExternalOpenBankingGetAccountBalanceResponse> GetAccountBalanceAsync(string accountId,
        string authToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);
        HttpResponseMessage response = await httpClient.GetAsync($"v1/accounts/{accountId}/balance", cancellationToken);

        // Throws rather than returning empty, so a refused request isn't recorded as a balance sync
        EnsureSuccess(response, "account balance");

        ExternalOpenBankingGetAccountBalanceResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingGetAccountBalanceResponse>(cancellationToken);

        return responseBody!;
    }

    public async Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountTransactionsAsync(string accountId,
        string authToken, DateTime? transactionsStartingDate,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        StringBuilder urlBuilder = new();
        urlBuilder.Append(CultureInfo.CurrentCulture, $"v1/accounts/{accountId}/transactions");

        if (transactionsStartingDate.HasValue)
        {
            urlBuilder.Append(CultureInfo.CurrentCulture, $"?from={transactionsStartingDate:yyyy-MM-dd}&to={_timeProvider.GetLocalNow():yyyy-MM-dd}");
        }

        // Throws rather than returning empty, so a refused request isn't recorded as a transaction sync
        HttpResponseMessage response = await httpClient.GetAsync(urlBuilder.ToString(), cancellationToken);
        EnsureSuccess(response, "transactions");

        ExternalOpenBankingAccountTransactionsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountTransactionsResponse>(cancellationToken);

        return responseBody!;
    }

    public async Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountPendingTransactionsAsync(
        string accountId, string authToken, DateTime? transactionsStartingDate,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        StringBuilder urlBuilder = new();
        urlBuilder.Append(CultureInfo.CurrentCulture, $"v1/accounts/{accountId}/transactions/pending");

        if (transactionsStartingDate.HasValue)
        {
            urlBuilder.Append(CultureInfo.CurrentCulture, $"?from={transactionsStartingDate:yyyy-MM-dd}&to={_timeProvider.GetLocalNow():yyyy-MM-dd}");
        }

        // Throws rather than returning empty, an empty pending list removes every stored pending transaction
        HttpResponseMessage response = await httpClient.GetAsync(urlBuilder.ToString(), cancellationToken);
        EnsureSuccess(response, "pending transactions");

        ExternalOpenBankingAccountTransactionsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountTransactionsResponse>(cancellationToken);

        return responseBody!;
    }

    public async Task<ExternalOpenBankingAccountStandingOrdersResponse> GetAccountStandingOrdersAsync(string accountId,
        string authToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        HttpResponseMessage response =
            await httpClient.GetAsync($"v1/accounts/{accountId}/standing_orders", cancellationToken);

        // Throws rather than returning empty, an empty list removes every stored standing order
        EnsureSuccess(response, "standing orders");

        ExternalOpenBankingAccountStandingOrdersResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountStandingOrdersResponse>(cancellationToken);

        return responseBody!;
    }

    public async Task<ExternalOpenBankingAccountDirectDebitsResponse> GetAccountDirectDebitsAsync(string accountId,
        string authToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        HttpResponseMessage response =
            await httpClient.GetAsync($"v1/accounts/{accountId}/direct_debits", cancellationToken);

        // Throws rather than returning empty, an empty list removes every stored direct debit
        EnsureSuccess(response, "direct debits");

        ExternalOpenBankingAccountDirectDebitsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountDirectDebitsResponse>(cancellationToken);

        return responseBody!;
    }

    public async IAsyncEnumerable<ExternalOpenBankingProvider> GetAvailableProvidersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseAuthUrl);

        HttpResponseMessage response =
            await httpClient.GetAsync($"api/providers?client_id={_trueLayerOpenBankingConfiguration.ClientId}",
                cancellationToken);

        // The list is cached, a failure has to throw rather than read as no providers
        EnsureSuccess(response, "providers");

        await foreach (ExternalOpenBankingProvider? provider in response.Content
                           .ReadFromJsonAsAsyncEnumerable<ExternalOpenBankingProvider>(cancellationToken))
        {
            if(provider is null)
                continue;

            yield return provider;
        }
    }

    public string BuildAuthUrl(IEnumerable<string> providerIds, IEnumerable<string> scopes)
    {
        StringBuilder sb = new();

        sb.Append(_trueLayerOpenBankingConfiguration.BaseAuthUrl);
        sb.Append(CultureInfo.CurrentCulture, $"?response_type=code&client_id={_trueLayerOpenBankingConfiguration.ClientId}");
        sb.Append('&');
        sb.Append(CultureInfo.CurrentCulture, $"scope={Uri.EscapeDataString(string.Join(" ", scopes))}");
        sb.Append(Uri.EscapeDataString(" "));


        sb.Append(CultureInfo.CurrentCulture, $"&redirect_uri={_trueLayerOpenBankingConfiguration.AuthRedirectUrl}");
        sb.Append(CultureInfo.CurrentCulture, $"&providers={Uri.EscapeDataString(string.Join(" ", providerIds))}");

        sb.Append(Uri.EscapeDataString(" "));

        sb.Append("uk-oauth-all");

        return sb.ToString();
    }

    public async Task<ExternalOpenBankingAccountConnectionResponse> GetProviderInformation(string accessToken,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient =
            await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, accessToken);

        HttpResponseMessage response = await httpClient.GetAsync("v1/me", cancellationToken);
        if (!Succeeded(response, "connection information"))
        {
            return new ExternalOpenBankingAccountConnectionResponse();
        }

        ExternalOpenBankingAccountConnectionResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountConnectionResponse>(cancellationToken);

        return responseBody!;
    }

    public async Task<byte[]> GetProviderLogoAsync(string logoUri, CancellationToken cancellationToken)
    {
        // Logos are public assets on their own host, no base address or auth headers
        using HttpClient httpClient = _clientFactory.CreateClient(HttpClientName);

        return await httpClient.GetByteArrayAsync(logoUri, cancellationToken);
    }

    private async Task<HttpClient> BuildHttpClient(Uri baseUrl, string? authHeader = null)
    {
        HttpClient httpClient = _clientFactory.CreateClient(HttpClientName);
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
    private bool Succeeded(HttpResponseMessage response, string requested)
    {
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        _logger.LogTrueLayerRequestRefused(requested, (int)response.StatusCode, response.ReasonPhrase,
            TraceId(response));
        return false;
    }

    /// <summary>
    /// Throws when TrueLayer refused the request, after logging it. The message carries the status and TrueLayer's
    /// trace id.
    /// </summary>
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

    private static string TraceId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("tl-trace-id", out IEnumerable<string>? traceIds))
        {
            return string.Join(",", traceIds);
        }

        return "none";
    }
}
