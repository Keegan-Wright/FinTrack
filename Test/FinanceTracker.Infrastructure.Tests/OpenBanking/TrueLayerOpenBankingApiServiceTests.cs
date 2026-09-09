using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Tests.Shared;

namespace FinanceTracker.Infrastructure.Tests.OpenBanking;

public class TrueLayerOpenBankingApiServiceTests : TestFixtureBase
{
    [Test]
    public async Task ExchangeCodeForAccessTokenAsync_ValidRequest_ReturnsMappedResponse()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var accessResponse = new ExternalOpenBankingAccessResponse
        {
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresIn = 3600,
            TokenType = "Bearer"
        };

        var subject = BuildSubject(async request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(accessResponse)
            };
        });

        // Act
        ExternalOpenBankingAccessResponse result =
            await subject.ExchangeCodeForAccessTokenAsync("vendor-code", _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.AccessToken).IsEqualTo("token");
            await Assert.That(result.RefreshToken).IsEqualTo("refresh");
            await Assert.That(capturedRequest).IsNotNull();
            await Assert.That(capturedRequest!.Method).IsEqualTo(HttpMethod.Post);
            await Assert.That(capturedRequest.RequestUri!.ToString()).EndsWith("connect/token");

            var formBody = await capturedRequest.Content!.ReadAsStringAsync(_cancellationTokenSource.Token);
            await Assert.That(formBody).Contains("grant_type=authorization_code");
            await Assert.That(formBody).Contains("code=vendor-code");
            await Assert.That(formBody).Contains("client_id=client-id");
        }
    }

    [Test]
    public async Task GetProviderInformation_FailedRequest_ReturnsDefaultResponse()
    {
        // Arrange
        var subject = BuildSubject(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        // Act
        ExternalOpenBankingAccountConnectionResponse result =
            await subject.GetProviderInformation("access-token", _cancellationTokenSource.Token);

        // Assert
        await Assert.That(result.Results).IsNull();
    }

    [Test]
    public async Task GetAvailableProvidersAsync_NullEntriesInPayload_FiltersNullValues()
    {
        // Arrange
        var providers = new ExternalOpenBankingProvider?[]
        {
            new(),
            null,
            new()
        };

        var subject = BuildSubject(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(providers)
            }));

        // Act
        List<ExternalOpenBankingProvider> result = [];
        await foreach (ExternalOpenBankingProvider provider in subject.GetAvailableProvidersAsync(_cancellationTokenSource.Token))
        {
            result.Add(provider);
        }

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task BuildAuthUrl_ValidProvidersAndScopes_BuildsExpectedUrl()
    {
        // Arrange
        var subject = BuildSubject(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Act
        var result = subject.BuildAuthUrl(["providerA", "providerB"], ["info", "accounts"]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("https://auth.example.com/?response_type=code&client_id=client-id");
            await Assert.That(result).Contains("scope=info%20accounts%20");
            await Assert.That(result).Contains("redirect_uri=https://localhost/callback");
            await Assert.That(result).Contains("providers=providerA%20providerB%20uk-oauth-all");
        }
    }

    private static TrueLayerOpenBankingApiService BuildSubject(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
    {
        var configuration = new TrueLayerOpenBankingConfiguration
        {
            BaseAuthUrl = new Uri("https://auth.example.com/"),
            BaseDataUrl = new Uri("https://data.example.com/"),
            AuthRedirectUrl = new Uri("https://localhost/callback"),
            ClientId = "client-id",
            ClientSecret = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            PublicIpAddress = "127.0.0.1"
        };

        HttpClient client = new(new FakeHttpMessageHandler(responseFactory));
        var clientFactory = new FakeHttpClientFactory(client);

        return new TrueLayerOpenBankingApiService(configuration, clientFactory);
    }

    private sealed class FakeHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request);
    }
}
