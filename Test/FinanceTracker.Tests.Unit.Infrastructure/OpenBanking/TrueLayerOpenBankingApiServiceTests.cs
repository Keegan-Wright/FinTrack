using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Tests.Unit.Infrastructure.OpenBanking;

public class TrueLayerOpenBankingApiServiceTests : UnitTestBase
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

        TrueLayerOpenBankingApiService subject = BuildSubject(async request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(accessResponse)
            };
        });

        // Act
        ExternalOpenBankingAccessResponse result =
            await subject.ExchangeCodeForAccessTokenAsync("vendor-code", CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.AccessToken).IsEqualTo("token");
            await Assert.That(result.RefreshToken).IsEqualTo("refresh");
            await Assert.That(capturedRequest).IsNotNull();
            await Assert.That(capturedRequest!.Method).IsEqualTo(HttpMethod.Post);
            await Assert.That(capturedRequest.RequestUri!.ToString()).EndsWith("connect/token");

            string formBody = await capturedRequest.Content!.ReadAsStringAsync(CancellationToken);
            await Assert.That(formBody).Contains("grant_type=authorization_code");
            await Assert.That(formBody).Contains("code=vendor-code");
            await Assert.That(formBody).Contains("client_id=client-id");
        }
    }

    [Test]
    public async Task ExchangeCodeForAccessTokenAsync_RejectedCode_Throws()
    {
        // Arrange
        TrueLayerOpenBankingApiService subject = BuildSubject(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(new { error = "invalid_grant" })
            }));

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            subject.ExchangeCodeForAccessTokenAsync("expired-code", CancellationToken));
    }

    [Test]
    public async Task GetProviderInformation_FailedRequest_ReturnsDefaultResponse()
    {
        // Arrange
        TrueLayerOpenBankingApiService subject = BuildSubject(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        // Act
        ExternalOpenBankingAccountConnectionResponse result =
            await subject.GetProviderInformation("access-token", CancellationToken);

        // Assert
        await Assert.That((object?)result.Results).IsNull();
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

        TrueLayerOpenBankingApiService subject = BuildSubject(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(providers)
            }));

        // Act
        List<ExternalOpenBankingProvider> result = [];
        await foreach (ExternalOpenBankingProvider provider in subject.GetAvailableProvidersAsync(CancellationToken))
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
        TrueLayerOpenBankingApiService subject = BuildSubject(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Act
        string result = subject.BuildAuthUrl(["providerA", "providerB"], ["info", "accounts"]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result).Contains("https://auth.example.com/?response_type=code&client_id=client-id");
            await Assert.That(result).Contains("scope=info%20accounts%20");
            await Assert.That(result).Contains("redirect_uri=https://localhost/callback");
            await Assert.That(result).Contains("providers=providerA%20providerB%20uk-oauth-all");
        }
    }

    [Test]
    public async Task GetAccessTokenByRefreshTokenAsync_PostsRefreshTokenToTokenEndpoint()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        string? formBody = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(async request =>
        {
            capturedRequest = request;
            formBody = await request.Content!.ReadAsStringAsync();
            return Json(new ExternalOpenBankingAccessResponse { AccessToken = "new-token", RefreshToken = "new-refresh" });
        });

        // Act
        ExternalOpenBankingAccessResponse result =
            await subject.GetAccessTokenByRefreshTokenAsync("refresh-token", CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.AccessToken).IsEqualTo("new-token");
            await Assert.That(capturedRequest!.Method).IsEqualTo(HttpMethod.Post);
            await Assert.That(capturedRequest.RequestUri!.ToString()).IsEqualTo("https://auth.example.com/connect/token");
            await Assert.That(formBody).Contains("grant_type=refresh_token");
            await Assert.That(formBody).Contains("refresh_token=refresh-token");
            await Assert.That(formBody).Contains("client_id=client-id");
        }
    }

    [Test]
    public async Task GetProviderLogoAsync_DownloadsLogoWithoutAuthHeaders()
    {
        // Arrange
        byte[] logo = [137, 80, 78, 71];
        HttpRequestMessage? capturedRequest = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(request =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(logo) });
        });

        // Act
        byte[] result = await subject.GetProviderLogoAsync("https://logos.example.com/bank.png", CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result).IsEquivalentTo(logo);
            await Assert.That(capturedRequest!.Method).IsEqualTo(HttpMethod.Get);
            await Assert.That(capturedRequest.RequestUri!.ToString()).IsEqualTo("https://logos.example.com/bank.png");
            await Assert.That(capturedRequest.Headers.Authorization).IsNull();
        }
    }

    [Test]
    public async Task GetProviderLogoAsync_FailedRequest_Throws()
    {
        TrueLayerOpenBankingApiService subject = BuildSubject(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            subject.GetProviderLogoAsync("https://logos.example.com/missing.png", CancellationToken));
    }

    [Test]
    public async Task GetAllAccountsAsync_ValidRequest_SendsAuthHeadersAndReturnsAccounts()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(request =>
        {
            capturedRequest = request;
            return Task.FromResult(Json(new
            {
                results = new[] { new ExternalOpenBankingAccount { AccountId = "acc-1", DisplayName = "Main" } },
                status = "Succeeded"
            }));
        });

        // Act
        ExternalOpenBankingListAllAccountsResponse result =
            await subject.GetAllAccountsAsync("auth-token", CancellationToken);

        // Assert
        List<ExternalOpenBankingAccount> accounts = await result.Results!.ToListAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(capturedRequest!.Method).IsEqualTo(HttpMethod.Get);
            await Assert.That(capturedRequest.RequestUri!.ToString()).IsEqualTo("https://data.example.com/v1/accounts");
            await Assert.That(capturedRequest.Headers.Authorization!.Scheme).IsEqualTo("Bearer");
            await Assert.That(capturedRequest.Headers.Authorization.Parameter).IsEqualTo("auth-token");
            await Assert.That(capturedRequest.Headers.GetValues("x-PSU-IP").Single()).IsEqualTo("127.0.0.1");
            await Assert.That(accounts.Single().AccountId).IsEqualTo("acc-1");
            await Assert.That(accounts.Single().DisplayName).IsEqualTo("Main");
        }
    }

    [Test]
    public async Task GetAccountBalanceAsync_ValidRequest_RequestsAccountBalance()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(request =>
        {
            capturedRequest = request;
            return Task.FromResult(Json(new
            {
                results = new[] { new ExternalOpenBankingAccountBalance { Available = 10, Current = 20 } }
            }));
        });

        // Act
        ExternalOpenBankingGetAccountBalanceResponse result =
            await subject.GetAccountBalanceAsync("acc-1", "auth-token", CancellationToken);

        // Assert
        ExternalOpenBankingAccountBalance balance = (await result.Results!.ToListAsync(CancellationToken)).Single();

        using (Assert.Multiple())
        {
            await Assert.That(capturedRequest!.RequestUri!.ToString())
                .IsEqualTo("https://data.example.com/v1/accounts/acc-1/balance");
            await Assert.That(balance.Available).IsEqualTo(10m);
            await Assert.That(balance.Current).IsEqualTo(20m);
        }
    }

    [Test]
    public async Task GetAccountTransactionsAsync_WithStartingDate_RequestsDateRange()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(request =>
        {
            capturedRequest = request;
            return Task.FromResult(Json(new { results = Array.Empty<ExternalOpenBankingAccountTransaction>() }));
        });

        // Act
        await subject.GetAccountTransactionsAsync("acc-1", "auth-token", new DateTime(2026, 1, 5), CancellationToken);

        // Assert
        await Assert.That(capturedRequest!.RequestUri!.ToString()).IsEqualTo(
            $"https://data.example.com/v1/accounts/acc-1/transactions?from=2026-01-05&to={DateTime.Now:yyyy-MM-dd}");
    }

    [Test]
    public async Task GetAccountTransactionsAsync_WithoutStartingDate_RequestsAllTransactions()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(request =>
        {
            capturedRequest = request;
            return Task.FromResult(Json(new
            {
                results = new[] { new ExternalOpenBankingAccountTransaction { TransactionId = "tx-1", Amount = -5 } }
            }));
        });

        // Act
        ExternalOpenBankingAccountTransactionsResponse result =
            await subject.GetAccountTransactionsAsync("acc-1", "auth-token", null, CancellationToken);

        // Assert
        ExternalOpenBankingAccountTransaction transaction = (await result.Results!.ToListAsync(CancellationToken)).Single();

        using (Assert.Multiple())
        {
            await Assert.That(capturedRequest!.RequestUri!.ToString())
                .IsEqualTo("https://data.example.com/v1/accounts/acc-1/transactions");
            await Assert.That(transaction.TransactionId).IsEqualTo("tx-1");
            await Assert.That(transaction.Amount).IsEqualTo(-5m);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task GetAccountPendingTransactionsAsync_RequestsPendingEndpoint(bool withStartingDate)
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(request =>
        {
            capturedRequest = request;
            return Task.FromResult(Json(new { results = Array.Empty<ExternalOpenBankingAccountTransaction>() }));
        });

        // Act
        await subject.GetAccountPendingTransactionsAsync("acc-1", "auth-token",
            withStartingDate ? new DateTime(2026, 1, 5) : null, CancellationToken);

        // Assert
        string expected = "https://data.example.com/v1/accounts/acc-1/transactions/pending" +
                          (withStartingDate ? $"?from=2026-01-05&to={DateTime.Now:yyyy-MM-dd}" : string.Empty);
        await Assert.That(capturedRequest!.RequestUri!.ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task GetAccountStandingOrdersAsync_ValidRequest_RequestsStandingOrders()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(request =>
        {
            capturedRequest = request;
            return Task.FromResult(Json(new
            {
                results = new[] { new ExternalOpenBankingAccountStandingOrder { Reference = "REF-1", Payee = "Landlord" } }
            }));
        });

        // Act
        ExternalOpenBankingAccountStandingOrdersResponse result =
            await subject.GetAccountStandingOrdersAsync("acc-1", "auth-token", CancellationToken);

        // Assert
        ExternalOpenBankingAccountStandingOrder standingOrder = (await result.Results!.ToListAsync(CancellationToken)).Single();

        using (Assert.Multiple())
        {
            await Assert.That(capturedRequest!.RequestUri!.ToString())
                .IsEqualTo("https://data.example.com/v1/accounts/acc-1/standing_orders");
            await Assert.That(standingOrder.Reference).IsEqualTo("REF-1");
            await Assert.That(standingOrder.Payee).IsEqualTo("Landlord");
        }
    }

    [Test]
    public async Task GetAccountDirectDebitsAsync_ValidRequest_RequestsDirectDebits()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(request =>
        {
            capturedRequest = request;
            return Task.FromResult(Json(new
            {
                results = new[] { new ExternalOpenBankingDirectDebit { DirectDebitId = "dd-1", Name = "Energy" } }
            }));
        });

        // Act
        ExternalOpenBankingAccountDirectDebitsResponse result =
            await subject.GetAccountDirectDebitsAsync("acc-1", "auth-token", CancellationToken);

        // Assert
        ExternalOpenBankingDirectDebit directDebit = (await result.Results!.ToListAsync(CancellationToken)).Single();

        using (Assert.Multiple())
        {
            await Assert.That(capturedRequest!.RequestUri!.ToString())
                .IsEqualTo("https://data.example.com/v1/accounts/acc-1/direct_debits");
            await Assert.That(directDebit.DirectDebitId).IsEqualTo("dd-1");
            await Assert.That(directDebit.Name).IsEqualTo("Energy");
        }
    }

    [Test]
    public async Task GetAllAccountsAsync_FailedRequest_ReturnsEmptyResponse()
    {
        TrueLayerOpenBankingApiService subject = BuildSubject(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        ExternalOpenBankingListAllAccountsResponse result = await subject.GetAllAccountsAsync("token", CancellationToken);

        await Assert.That((object?)result.Results).IsNull();
    }

    [Test]
    public async Task GetAccountBalanceAsync_FailedRequest_ThrowsWithTheTraceId()
    {
        TrueLayerOpenBankingApiService subject = BuildSubject(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.Add("tl-trace-id", "trace-123");
            return Task.FromResult(response);
        });

        HttpRequestException? thrown = await Assert.ThrowsAsync<HttpRequestException>(() =>
            subject.GetAccountBalanceAsync("acc-1", "token", CancellationToken));

        using (Assert.Multiple())
        {
            await Assert.That(thrown!.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
            await Assert.That(thrown.Message).Contains("429").And.Contains("tl-trace-id trace-123");
        }
    }

    [Test]
    public async Task GetAllAccountsAsync_RefusedRequest_LogsItWithTheTraceId()
    {
        var logger = new CapturingLogger<TrueLayerOpenBankingApiService>();
        TrueLayerOpenBankingApiService subject = BuildSubject(_ => Task.FromResult(TooManyRequests()), logger);

        await subject.GetAllAccountsAsync("token", CancellationToken);

        (LogLevel Level, string Message, Exception? Exception) entry = logger.Entries.Single();
        using (Assert.Multiple())
        {
            await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
            await Assert.That(entry.Message).Contains("accounts").And.Contains("429")
                .And.Contains("tl-trace-id trace-123");
        }
    }

    [Test]
    public async Task GetAccountBalanceAsync_RefusedRequest_LogsItBeforeThrowing()
    {
        var logger = new CapturingLogger<TrueLayerOpenBankingApiService>();
        TrueLayerOpenBankingApiService subject = BuildSubject(_ => Task.FromResult(TooManyRequests()), logger);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            subject.GetAccountBalanceAsync("acc-1", "token", CancellationToken));

        await Assert.That(logger.Entries.Single().Message).Contains("account balance").And.Contains("trace-123");
    }

    [Test]
    public async Task GetAccountDirectDebitsAsync_AcceptedRequest_LogsNothing()
    {
        var logger = new CapturingLogger<TrueLayerOpenBankingApiService>();
        TrueLayerOpenBankingApiService subject =
            BuildSubject(_ => Task.FromResult(Json(new { results = Array.Empty<object>() })), logger);

        await subject.GetAccountDirectDebitsAsync("acc-1", "token", CancellationToken);

        await Assert.That(logger.Entries).IsEmpty();
    }

    [Test]
    public async Task GetAccountTransactionsAsync_FailedRequest_Throws()
    {
        // An empty response would be recorded as a sync, moving where the next one fetches from
        TrueLayerOpenBankingApiService subject = BuildSubject(_ => Task.FromResult(TooManyRequests()));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            subject.GetAccountTransactionsAsync("acc-1", "token", null, CancellationToken));
    }

    [Test]
    public async Task GetAccountPendingTransactionsAsync_FailedRequest_Throws()
    {
        // An empty response would remove every stored pending transaction, so a failure must not look like one
        TrueLayerOpenBankingApiService subject = BuildSubject(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            subject.GetAccountPendingTransactionsAsync("acc-1", "token", null, CancellationToken));
    }

    [Test]
    public async Task GetAccountStandingOrdersAsync_FailedRequest_Throws()
    {
        // An empty response would remove every stored standing order, so a failure must not look like one
        TrueLayerOpenBankingApiService subject = BuildSubject(_ => Task.FromResult(TooManyRequests()));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            subject.GetAccountStandingOrdersAsync("acc-1", "token", CancellationToken));
    }

    [Test]
    public async Task GetAccountDirectDebitsAsync_FailedRequest_Throws()
    {
        // An empty response would remove every stored direct debit, so a failure must not look like one
        TrueLayerOpenBankingApiService subject = BuildSubject(_ => Task.FromResult(TooManyRequests()));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            subject.GetAccountDirectDebitsAsync("acc-1", "token", CancellationToken));
    }

    [Test]
    public async Task GetProviderInformation_ValidRequest_RequestsMeEndpoint()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(request =>
        {
            capturedRequest = request;
            return Task.FromResult(Json(new
            {
                results = new[]
                {
                    new
                    {
                        provider = new ExternalOpenBankingAccountConnectionProvider { ProviderId = "ob-1" },
                        scopes = new[] { "accounts" }
                    }
                }
            }));
        });

        // Act
        ExternalOpenBankingAccountConnectionResponse result =
            await subject.GetProviderInformation("access-token", CancellationToken);

        // Assert
        ExternalOpenBankingAccountConnection connection = (await result.Results!.ToListAsync(CancellationToken)).Single();

        using (Assert.Multiple())
        {
            await Assert.That(capturedRequest!.RequestUri!.ToString()).IsEqualTo("https://data.example.com/v1/me");
            await Assert.That(capturedRequest.Headers.Authorization!.Parameter).IsEqualTo("access-token");
            await Assert.That(connection.Provider!.ProviderId).IsEqualTo("ob-1");
            await Assert.That(await connection.Scopes!.ToListAsync(CancellationToken)).IsEquivalentTo(["accounts"]);
        }
    }

    [Test]
    public async Task GetAvailableProvidersAsync_RequestsProvidersForClient()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        TrueLayerOpenBankingApiService subject = BuildSubject(request =>
        {
            capturedRequest = request;
            return Task.FromResult(Json(Array.Empty<ExternalOpenBankingProvider>()));
        });

        // Act
        await subject.GetAvailableProvidersAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(capturedRequest!.RequestUri!.ToString())
                .IsEqualTo("https://auth.example.com/api/providers?client_id=client-id");
            await Assert.That(capturedRequest.Headers.Authorization).IsNull();
        }
    }

    private static HttpResponseMessage TooManyRequests()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.Add("tl-trace-id", "trace-123");
        return response;
    }

    private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static TrueLayerOpenBankingApiService BuildSubject(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory,
        ILogger<TrueLayerOpenBankingApiService>? logger = null)
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

        return new TrueLayerOpenBankingApiService(configuration, clientFactory, TimeProvider.System,
            logger ?? NullLogger<TrueLayerOpenBankingApiService>.Instance);
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
