using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using FinanceTracker.Configurations;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Models.External;

namespace FinanceTracker.Services.External.OpenBanking;

[InjectionCategory(InjectionCategoryType.External)]
[Scoped<IOpenBankingApiService>]
public class TrueLayerOpenBankingApiService : IOpenBankingApiService
{
    private readonly TrueLayerOpenBankingConfiguration _trueLayerOpenBankingConfiguration;

    public TrueLayerOpenBankingApiService(TrueLayerOpenBankingConfiguration trueLayerOpenBankingConfiguration) =>
        _trueLayerOpenBankingConfiguration = trueLayerOpenBankingConfiguration;


    public async Task<ExternalOpenBankingAccessResponse> ExchangeCodeForAccessTokenAsync(string vendorAccessCode,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseAuthUrl);

        List<KeyValuePair<string, string>> formData = new()
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("client_id", _trueLayerOpenBankingConfiguration.ClientId),
            new KeyValuePair<string, string>("client_secret",
                _trueLayerOpenBankingConfiguration.ClientSecret.ToString()),
            new KeyValuePair<string, string>("redirect_uri", _trueLayerOpenBankingConfiguration.AuthRedirectUrl),
            new KeyValuePair<string, string>("code", vendorAccessCode)
        };

        HttpContent content = new FormUrlEncodedContent(formData);

        HttpResponseMessage response = await httpClient.PostAsync("connect/token", content, cancellationToken);

        ExternalOpenBankingAccessResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccessResponse>(cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingAccessResponse> GetAccessTokenByRefreshTokenAsync(string refreshToken,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseAuthUrl);

        List<KeyValuePair<string, string>> formData = new()
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("client_id", _trueLayerOpenBankingConfiguration.ClientId),
            new KeyValuePair<string, string>("client_secret",
                _trueLayerOpenBankingConfiguration.ClientSecret.ToString()),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        };

        HttpContent content = new FormUrlEncodedContent(formData);

        HttpResponseMessage response = await httpClient.PostAsync("connect/token", content, cancellationToken);

        ExternalOpenBankingAccessResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccessResponse>(cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingListAllAccountsResponse> GetAllAccountsAsync(string authToken,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        HttpResponseMessage response = await httpClient.GetAsync("v1/accounts", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalOpenBankingListAllAccountsResponse();
        }

        ExternalOpenBankingListAllAccountsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingListAllAccountsResponse>(cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingGetAccountBalanceResponse> GetAccountBalanceAsync(string accountId,
        string authToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);
        HttpResponseMessage response = await httpClient.GetAsync($"v1/accounts/{accountId}/balance", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalOpenBankingGetAccountBalanceResponse();
        }

        ExternalOpenBankingGetAccountBalanceResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingGetAccountBalanceResponse>(cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountTransactionsAsync(string accountId,
        string authToken, DateTime? transactionsStartingDate,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        StringBuilder urlBuilder = new();
        urlBuilder.Append($"v1/accounts/{accountId}/transactions");

        if (transactionsStartingDate.HasValue)
        {
            urlBuilder.Append($"?from={transactionsStartingDate:yyyy-MM-dd}&to={DateTime.Now:yyyy-MM-dd}");
        }

        HttpResponseMessage response = await httpClient.GetAsync(urlBuilder.ToString(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalOpenBankingAccountTransactionsResponse();
        }

        ExternalOpenBankingAccountTransactionsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountTransactionsResponse>(cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountPendingTransactionsAsync(
        string accountId, string authToken, DateTime? transactionsStartingDate,
        CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        StringBuilder urlBuilder = new();
        urlBuilder.Append($"v1/accounts/{accountId}/transactions/pending");

        if (transactionsStartingDate.HasValue)
        {
            urlBuilder.Append($"?from={transactionsStartingDate:yyyy-MM-dd}&to={DateTime.Now:yyyy-MM-dd}");
        }

        HttpResponseMessage response = await httpClient.GetAsync(urlBuilder.ToString(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalOpenBankingAccountTransactionsResponse();
        }

        ExternalOpenBankingAccountTransactionsResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountTransactionsResponse>(cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingAccountStandingOrdersResponse> GetAccountStandingOrdersAsync(string accountId,
        string authToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        HttpResponseMessage response =
            await httpClient.GetAsync($"v1/accounts/{accountId}/standing_orders", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalOpenBankingAccountStandingOrdersResponse();
        }

        if (response.IsSuccessStatusCode)
        {
            ExternalOpenBankingAccountStandingOrdersResponse? responseBody =
                await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountStandingOrdersResponse>(
                    cancellationToken);

            return responseBody;
        }

        return new ExternalOpenBankingAccountStandingOrdersResponse();
    }

    public async Task<ExternalOpenBankingAccountDirectDebitsResponse> GetAccountDirectDebitsAsync(string accountId,
        string authToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        HttpResponseMessage response =
            await httpClient.GetAsync($"v1/accounts/{accountId}/direct_debits", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalOpenBankingAccountDirectDebitsResponse();
        }

        if (response.IsSuccessStatusCode)
        {
            ExternalOpenBankingAccountDirectDebitsResponse? responseBody =
                await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountDirectDebitsResponse>(
                    cancellationToken);
            return responseBody;
        }

        return new ExternalOpenBankingAccountDirectDebitsResponse();
    }

    public async IAsyncEnumerable<ExternalOpenBankingProvider> GetAvailableProvidersAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseAuthUrl);

        HttpResponseMessage response =
            await httpClient.GetAsync($"api/providers?client_id={_trueLayerOpenBankingConfiguration.ClientId}",
                cancellationToken);

        await foreach (ExternalOpenBankingProvider? provider in response.Content
                           .ReadFromJsonAsAsyncEnumerable<ExternalOpenBankingProvider>(cancellationToken))
        {
            yield return provider;
        }
    }

    public string BuildAuthUrl(IEnumerable<string> providerIds, IEnumerable<string> scopes)
    {
        StringBuilder sb = new();

        sb.Append(_trueLayerOpenBankingConfiguration.BaseAuthUrl);
        sb.Append($"?response_type=code&client_id={_trueLayerOpenBankingConfiguration.ClientId}");
        sb.Append("&");
        sb.Append($"scope={Uri.EscapeDataString(string.Join(" ", scopes))}");
        sb.Append(Uri.EscapeDataString(" "));


        sb.Append($"&redirect_uri={_trueLayerOpenBankingConfiguration.AuthRedirectUrl}");
        sb.Append($"&providers={Uri.EscapeDataString(string.Join(" ", providerIds))}");

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
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalOpenBankingAccountConnectionResponse();
        }

        ExternalOpenBankingAccountConnectionResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountConnectionResponse>(cancellationToken);

        return responseBody;
    }

    private async Task<HttpClient> BuildHttpClient(string baseUrl, string? authHeader = null)
    {
        //var httpHandler = new SentryHttpMessageHandler();
        HttpClient httpClient = new();
        httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(authHeader))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authHeader);
        }

        IPHostEntry dnsEntries = await Dns.GetHostEntryAsync(Dns.GetHostName());
        httpClient.DefaultRequestHeaders.Add("x-PSU-IP",
            dnsEntries.AddressList.FirstOrDefault(x =>
                x.AddressFamily == AddressFamily.InterNetwork && x.MapToIPv4().ToString() != "127.0.1.1").ToString());

        httpClient.Timeout = TimeSpan.FromMinutes(5);

        return httpClient;
    }
}
