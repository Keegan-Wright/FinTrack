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
    public TrueLayerOpenBankingApiService(TrueLayerOpenBankingConfiguration trueLayerOpenBankingConfiguration)
    {
        _trueLayerOpenBankingConfiguration = trueLayerOpenBankingConfiguration;
    }


    public async Task<ExternalOpenBankingAccessResponse> ExchangeCodeForAccessTokenAsync(string vendorAccessCode, CancellationToken cancellationToken)
    {
        using var httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseAuthUrl);

        var formData = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("client_id", _trueLayerOpenBankingConfiguration.ClientId),
            new KeyValuePair<string, string>("client_secret", _trueLayerOpenBankingConfiguration.ClientSecret.ToString()),
            new KeyValuePair<string, string>("redirect_uri", _trueLayerOpenBankingConfiguration.AuthRedirectUrl),
            new KeyValuePair<string, string>("code", vendorAccessCode)
        };

        HttpContent content = new FormUrlEncodedContent(formData);

        var response = await httpClient.PostAsync("connect/token", content, cancellationToken);
        
        var responseBody = await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccessResponse>(cancellationToken: cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingAccessResponse> GetAccessTokenByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseAuthUrl);

        var formData = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("client_id", _trueLayerOpenBankingConfiguration.ClientId),
            new KeyValuePair<string, string>("client_secret", _trueLayerOpenBankingConfiguration.ClientSecret.ToString()),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
        };

        HttpContent content = new FormUrlEncodedContent(formData);

        var response = await httpClient.PostAsync("connect/token", content, cancellationToken);

        var responseBody = await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccessResponse>(cancellationToken: cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingListAllAccountsResponse> GetAllAccountsAsync(string authToken, CancellationToken cancellationToken)
    {
        using var httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        var response = await httpClient.GetAsync("v1/accounts", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new  ();
        
        var responseBody = await response.Content.ReadFromJsonAsync<ExternalOpenBankingListAllAccountsResponse>(cancellationToken: cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingGetAccountBalanceResponse> GetAccountBalanceAsync(string accountId, string authToken, CancellationToken cancellationToken)
    {
        using var httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);
        var response = await httpClient.GetAsync($"v1/accounts/{accountId}/balance", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new ();
        var responseBody = await response.Content.ReadFromJsonAsync<ExternalOpenBankingGetAccountBalanceResponse>(cancellationToken: cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountTransactionsAsync(string accountId, string authToken, DateTime? transactionsStartingDate,
        CancellationToken cancellationToken)
    {
        using var httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        var urlBuilder = new StringBuilder();
        urlBuilder.Append($"v1/accounts/{accountId}/transactions");

        if (transactionsStartingDate.HasValue)
        {
            urlBuilder.Append($"?from={transactionsStartingDate:yyyy-MM-dd}&to={DateTime.Now:yyyy-MM-dd}");
        }

        var response = await httpClient.GetAsync(urlBuilder.ToString(), cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new ();
        var responseBody = await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountTransactionsResponse>(cancellationToken: cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingAccountTransactionsResponse> GetAccountPendingTransactionsAsync(string accountId, string authToken, DateTime? transactionsStartingDate,
        CancellationToken cancellationToken)
    {
        using var httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        var urlBuilder = new StringBuilder();
        urlBuilder.Append($"v1/accounts/{accountId}/transactions/pending");

        if (transactionsStartingDate.HasValue)
        {
            urlBuilder.Append($"?from={transactionsStartingDate:yyyy-MM-dd}&to={DateTime.Now:yyyy-MM-dd}");
        }

        var response = await httpClient.GetAsync(urlBuilder.ToString(), cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new ();
        var responseBody = await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountTransactionsResponse>(cancellationToken: cancellationToken);

        return responseBody;
    }

    public async Task<ExternalOpenBankingAccountStandingOrdersResponse> GetAccountStandingOrdersAsync(string accountId, string authToken, CancellationToken cancellationToken)
    {
        using var httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        var response = await httpClient.GetAsync($"v1/accounts/{accountId}/standing_orders", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new ();
        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountStandingOrdersResponse>(cancellationToken: cancellationToken);

            return responseBody;
        }
        else
        {
            return new ExternalOpenBankingAccountStandingOrdersResponse();
        }

    }

    public async Task<ExternalOpenBankingAccountDirectDebitsResponse> GetAccountDirectDebitsAsync(string accountId, string authToken, CancellationToken cancellationToken)
    {
        using var httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        var response = await httpClient.GetAsync($"v1/accounts/{accountId}/direct_debits", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new ();
        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountDirectDebitsResponse>(cancellationToken: cancellationToken);
            return responseBody;
        }
        else
        {
            return new ExternalOpenBankingAccountDirectDebitsResponse();
        }

    }

    public async IAsyncEnumerable<ExternalOpenBankingProvider> GetAvailableProvidersAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseAuthUrl);

        var response = await httpClient.GetAsync($"api/providers?client_id={_trueLayerOpenBankingConfiguration.ClientId}", cancellationToken);

        await foreach (var provider in response.Content.ReadFromJsonAsAsyncEnumerable<ExternalOpenBankingProvider>(cancellationToken: cancellationToken))
        {
            yield return provider;
        }
    }

    public string BuildAuthUrl(IEnumerable<string> providerIds, IEnumerable<string> scopes)
    {
        var sb = new StringBuilder();

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

    public async Task<ExternalOpenBankingAccountConnectionResponse> GetProviderInformation(string accessToken, CancellationToken cancellationToken)
    {
        using var httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, accessToken);

        var response = await httpClient.GetAsync("v1/me", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new ();
        var responseBody = await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountConnectionResponse>(cancellationToken: cancellationToken);

        return responseBody;
    }

    private async Task<HttpClient> BuildHttpClient(string baseUrl, string? authHeader = null)
    {
        //var httpHandler = new SentryHttpMessageHandler();
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(authHeader))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authHeader);
        }

        var dnsEntries = await Dns.GetHostEntryAsync(Dns.GetHostName());
        httpClient.DefaultRequestHeaders.Add("x-PSU-IP", dnsEntries.AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork && x.MapToIPv4().ToString() != "127.0.1.1").ToString());

        httpClient.Timeout = TimeSpan.FromMinutes(5);

        return httpClient;
    }
}