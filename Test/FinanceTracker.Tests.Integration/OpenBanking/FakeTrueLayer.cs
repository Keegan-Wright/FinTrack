using System.Text;
using System.Text.Json;
using FinanceTracker.Infrastructure.OpenBanking.TrueLayer.Contracts;
using WireMock.Logging;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;

namespace FinanceTracker.Tests.Integration.OpenBanking;

/// <summary>
/// An in process stand in for TrueLayer's auth and data APIs, the app under test is pointed at it through the
/// AppHost configuration. Data stubs are keyed on the bearer token, so each test only sees its own connection's
/// data even though a background sync visits every connection in the shared database. Anything unstubbed gets a
/// 404, which the app treats as a failed fetch.
/// </summary>
public sealed class FakeTrueLayer : IDisposable
{
    public const string ClientId = "fintrack-integration-tests";
    public static readonly Guid ClientSecret = Guid.Parse("8a0d1c3e-0b8f-4a59-9a43-3a2a4f1f2f10");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WireMockServer Server { get; } = WireMockServer.Start();

    public FakeTrueLayer() =>
        Server
            .Given(Request.Create().WithPath("/auth/api/providers").UsingGet())
            .RespondWith(Json(JsonSerializer.Serialize(new[]
            {
                new { provider_id = "ob-fake-bank", display_name = "Fake Bank", country = "uk", logo_url = "", scopes = new[] { "accounts", "balance", "transactions" } },
                new { provider_id = "ob-other-bank", display_name = "Other Bank", country = "uk", logo_url = "", scopes = new[] { "accounts" } }
            })));

    public Uri AuthUrl => new($"{Server.Url}/auth/");
    public Uri DataUrl => new($"{Server.Url}/data/");

    /// <summary>
    /// Serves <paramref name="account"/> and its data to requests made with <paramref name="accessToken"/>.
    /// </summary>
    public async Task StubAccountAsync(string accessToken, ExternalOpenBankingAccount account,
        IEnumerable<ExternalOpenBankingAccountBalance>? balances = null,
        IEnumerable<ExternalOpenBankingAccountTransaction>? transactions = null,
        IEnumerable<ExternalOpenBankingAccountTransaction>? pendingTransactions = null,
        IEnumerable<ExternalOpenBankingAccountStandingOrder>? standingOrders = null,
        IEnumerable<ExternalOpenBankingDirectDebit>? directDebits = null)
    {
        string accountPath = $"/data/v1/accounts/{account.AccountId}";

        await StubDataAsync(accessToken, "/data/v1/accounts", Results([account]));
        await StubDataAsync(accessToken, $"{accountPath}/balance", Results(balances ?? []));
        await StubDataAsync(accessToken, $"{accountPath}/transactions", Results(transactions ?? []));
        await StubDataAsync(accessToken, $"{accountPath}/transactions/pending", Results(pendingTransactions ?? []));
        await StubDataAsync(accessToken, $"{accountPath}/standing_orders", Results(standingOrders ?? []));
        await StubDataAsync(accessToken, $"{accountPath}/direct_debits", Results(directDebits ?? []));
    }

    /// <summary>
    /// Exchanges <paramref name="refreshToken"/> for <paramref name="response"/> on the token endpoint.
    /// </summary>
    public async Task StubRefreshAsync(string refreshToken, ExternalOpenBankingAccessResponse response)
    {
        Server
            .Given(Request.Create()
                .WithPath("/auth/connect/token")
                .UsingPost()
                .WithBody(new RegexMatcher(
                    $"^(?=.*grant_type=refresh_token)(?=.*refresh_token={refreshToken}(&|$)).*$")))
            .RespondWith(Json(await SerializeAsync(response)));
    }

    /// <summary>
    /// Requests the app made with <paramref name="accessToken"/>.
    /// </summary>
    public IReadOnlyList<ILogEntry> RequestsWithToken(string accessToken) =>
        Server.LogEntries
            .Where(x => x.RequestMessage?.Headers?.TryGetValue("Authorization", out WireMockList<string>? values) == true &&
                        values.Contains($"Bearer {accessToken}"))
            .ToList();

    /// <summary>
    /// Token endpoint requests that tried to use <paramref name="refreshToken"/>.
    /// </summary>
    public IReadOnlyList<ILogEntry> RefreshRequestsFor(string refreshToken) =>
        Server.LogEntries
            .Where(x => x.RequestMessage?.Path == "/auth/connect/token" &&
                        x.RequestMessage.Body?.Contains($"refresh_token={refreshToken}") == true)
            .ToList();

    public void Dispose() => Server.Stop();

    private async Task StubDataAsync(string accessToken, string path, object body) =>
        Server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("Authorization", $"Bearer {accessToken}"))
            .RespondWith(Json(await SerializeAsync(body)));

    private static IResponseBuilder Json(string body) =>
        Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(body);

    private static object Results<T>(IEnumerable<T> results) => new { results, status = "Succeeded" };

    /// <summary>
    /// The contracts expose <see cref="IAsyncEnumerable{T}"/> members, which only serialise asynchronously.
    /// </summary>
    private static async Task<string> SerializeAsync(object value)
    {
        using MemoryStream stream = new();
        await JsonSerializer.SerializeAsync(stream, value, value.GetType(), JsonOptions);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
