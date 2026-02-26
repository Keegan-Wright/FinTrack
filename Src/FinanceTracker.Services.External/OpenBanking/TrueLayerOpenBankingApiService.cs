using System.Globalization;
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
    private readonly IHttpClientFactory _clientFactory;

    public TrueLayerOpenBankingApiService(TrueLayerOpenBankingConfiguration trueLayerOpenBankingConfiguration, IHttpClientFactory clientFactory)
    {
        _trueLayerOpenBankingConfiguration = trueLayerOpenBankingConfiguration;
        _clientFactory = clientFactory;
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

        ExternalOpenBankingAccessResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccessResponse>(cancellationToken);

        return responseBody!;
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

        ExternalOpenBankingAccessResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccessResponse>(cancellationToken);

        return responseBody!;
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

        return responseBody!;
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
            urlBuilder.Append(CultureInfo.CurrentCulture, $"?from={transactionsStartingDate:yyyy-MM-dd}&to={DateTime.Now:yyyy-MM-dd}");
        }

        HttpResponseMessage response = await httpClient.GetAsync(urlBuilder.ToString(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalOpenBankingAccountTransactionsResponse();
        }

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
            urlBuilder.Append(CultureInfo.CurrentCulture, $"?from={transactionsStartingDate:yyyy-MM-dd}&to={DateTime.Now:yyyy-MM-dd}");
        }

        HttpResponseMessage response = await httpClient.GetAsync(urlBuilder.ToString(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalOpenBankingAccountTransactionsResponse();
        }

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
        switch (response.IsSuccessStatusCode)
        {
            case false:
                return new ExternalOpenBankingAccountStandingOrdersResponse();
            case true:
            {
                ExternalOpenBankingAccountStandingOrdersResponse? responseBody =
                    await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountStandingOrdersResponse>(
                        cancellationToken);

                return responseBody!;
            }
        }
    }

    public async Task<ExternalOpenBankingAccountDirectDebitsResponse> GetAccountDirectDebitsAsync(string accountId,
        string authToken, CancellationToken cancellationToken)
    {
        using HttpClient httpClient = await BuildHttpClient(_trueLayerOpenBankingConfiguration.BaseDataUrl, authToken);

        HttpResponseMessage response =
            await httpClient.GetAsync($"v1/accounts/{accountId}/direct_debits", cancellationToken);
        switch (response.IsSuccessStatusCode)
        {
            case false:
                return new ExternalOpenBankingAccountDirectDebitsResponse();
            case true:
            {
                ExternalOpenBankingAccountDirectDebitsResponse? responseBody =
                    await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountDirectDebitsResponse>(
                        cancellationToken);

                return responseBody!;
            }
        }
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
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalOpenBankingAccountConnectionResponse();
        }

        ExternalOpenBankingAccountConnectionResponse? responseBody =
            await response.Content.ReadFromJsonAsync<ExternalOpenBankingAccountConnectionResponse>(cancellationToken);

        return responseBody!;
    }

    private async Task<HttpClient> BuildHttpClient(Uri baseUrl, string? authHeader = null)
    {
        var httpClient = _clientFactory.CreateClient("OpenBankingClient");
        httpClient.BaseAddress = baseUrl;

        if (!string.IsNullOrEmpty(authHeader))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authHeader);
        }

        httpClient.DefaultRequestHeaders.Add("x-PSU-IP", _trueLayerOpenBankingConfiguration.PublicIpAddress);

        httpClient.Timeout = TimeSpan.FromMinutes(5);

        return httpClient;
    }
}
