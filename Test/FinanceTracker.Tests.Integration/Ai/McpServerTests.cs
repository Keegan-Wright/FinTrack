#if FINTRACK_AI
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceTracker.Ai.Contracts;
using FinanceTracker.Client.Ai;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Tokens;
using FinanceTracker.Tests.Integration.Http;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace FinanceTracker.Tests.Integration.Ai;

/// <summary>
/// The AI module's MCP server at /mcp, reached with a token made for AI clients and only such a token. In the AI
/// build only.
/// </summary>
[Category("AiModule")]
public class McpServerTests : IntegrationTestBase
{
    private const string McpPath = "/mcp";

    [Test]
    public async Task ListTools_WithAnAiToken_OffersEveryReadOnlyTool()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync(aiOn: true);
        await using McpClient client = await ConnectAsync(await TokenForAsync(user, TokenScope.Ai));

        // Act
        IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(tools.Select(static x => x.Name)).IsEquivalentTo([
                "list_accounts", "list_tags", "get_budgets", "get_totals", "get_spending_breakdown",
                "compare_periods", "get_bills", "get_upcoming_payments", "get_net_worth", "get_savings_goals",
                "search_transactions", "get_debts", "get_today"
            ]);
            await Assert.That(tools.All(static x => x.ProtocolTool.Annotations?.ReadOnlyHint == true)).IsTrue();
        }
    }

    [Test]
    public async Task CallTool_WithAnAiToken_AnswersForThatUser()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync(aiOn: true);
        await using McpClient client = await ConnectAsync(await TokenForAsync(user, TokenScope.Ai));

        // Act
        CallToolResult result = await client.CallToolAsync("get_debts", cancellationToken: CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsError).IsNotEqualTo(true);
            await Assert.That(result.Content).IsNotEmpty();
        }
    }

    [Test]
    public async Task Mcp_AiTokenButAiTurnedOff_IsRefusedAsSignedOut()
    {
        // Arrange, turned on then off again
        SignedInUser user = await RegisterUserAsync(aiOn: true);
        string token = await TokenForAsync(user, TokenScope.Ai);
        await Ai(user).ChangeOptInAsync(new ChangeAiOptIn { OptedIn = false }, CancellationToken);

        // Act
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, McpPath, token, Initialize());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That(response.Headers.WwwAuthenticate.ToString()).IsEqualTo("Bearer");
        }
    }

    [Test]
    public async Task Mcp_AiTokenButAiNeverTurnedOn_IsRefusedAsSignedOut()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string token = await TokenForAsync(user, TokenScope.Ai);

        // Act
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, McpPath, token, Initialize());

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    [Arguments("GET")]
    [Arguments("POST")]
    [Arguments("DELETE")]
    public async Task Mcp_NotAnMcpRequestWithoutAToken_IsRefusedNotSentToSignIn(string method)
    {
        // Act, a GET, or a POST that isn't JSON, matches none of the MCP server's own endpoints
        using HttpResponseMessage response = await SendAsync(new HttpMethod(method), McpPath, token: null,
            mcpHeaders: false);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That(response.Headers.WwwAuthenticate.ToString()).IsEqualTo("Bearer");
        }
    }

    [Test]
    public async Task Mcp_GetWithAnAiToken_IsNotAllowed()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync(aiOn: true);
        string token = await TokenForAsync(user, TokenScope.Ai);

        // Act
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, McpPath, token, mcpHeaders: false);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.MethodNotAllowed);
    }

    [Test]
    public async Task Mcp_WithAReadToken_IsRefused()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync(aiOn: true);
        string token = await TokenForAsync(user, TokenScope.Read);

        // Act
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, McpPath, token, Initialize());

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Mcp_WithoutAToken_IsRefused()
    {
        // Act
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, McpPath, token: null, Initialize());

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Mcp_WithTheBrowsersCookie_IsRefused()
    {
        // Arrange, signed in in the browser's way but with no token
        SignedInUser user = await RegisterUserAsync();
        using HttpRequestMessage request = new(HttpMethod.Post, McpPath);
        request.Content = JsonContent.Create(Initialize());

        // Act
        using HttpResponseMessage response =
            await user.Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Api_WithAnAiToken_IsRefused()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync(aiOn: true);
        string token = await TokenForAsync(user, TokenScope.Ai);

        // Act, an endpoint a reading token may use
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"/{ApiRoutes.Calendar}/events", token);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A new user, with AI turned on when <paramref name="aiOn"/>.
    /// </summary>
    private async Task<SignedInUser> RegisterUserAsync(bool aiOn)
    {
        SignedInUser user = await RegisterUserAsync();
        if (aiOn)
        {
            await Ai(user).ChangeOptInAsync(new ChangeAiOptIn { OptedIn = true }, CancellationToken);
        }

        return user;
    }

    /// <summary>
    /// The AI API as <paramref name="user"/>, through the browser's own client.
    /// </summary>
    private AiApiClient Ai(SignedInUser user) => new(ApiFor(user).Get<IHttpClientFactory>());

    /// <summary>
    /// An MCP client connected to the app with <paramref name="token"/>, as an AI client would be.
    /// </summary>
    private async Task<McpClient> ConnectAsync(string token)
    {
        HttpClientTransportOptions options = new()
        {
            Endpoint = new Uri(App.WebBaseAddress, McpPath),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" }
        };
        HttpClient http = new(new HttpClientHandler
        {
            // The app is served with the local ASP.NET development certificate, which may not be trusted
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
        HttpClientTransport transport = new(options, http, ownsHttpClient: true);
        return await McpClient.CreateAsync(transport, cancellationToken: CancellationToken);
    }

    /// <summary>
    /// A new token of <paramref name="scope"/> for <paramref name="user"/>, made in the database as the app will.
    /// </summary>
    /// <returns>The whole token, as the user would be shown it.</returns>
    private async Task<string> TokenForAsync(SignedInUser user, TokenScope scope)
    {
        IssuedToken issued = PersonalAccessTokenFormat.Issue();
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        context.PersonalAccessTokens.Add(new PersonalAccessToken(user.UserId, "Placeholder AI client",
            issued.LookupId, issued.SecretHash, expiresAt: null, scope));
        await context.SaveChangesAsync(CancellationToken);
        return issued.Token;
    }

    /// <summary>
    /// The first message an MCP client sends.
    /// </summary>
    private static object Initialize() => new
    {
        jsonrpc = "2.0",
        id = 1,
        method = "initialize",
        @params = new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = "placeholder-client", version = "1.0" }
        }
    };

    /// <summary>
    /// Sends a request with only <paramref name="token"/>, no cookie and none of the app's headers, accepting what an
    /// MCP client does unless <paramref name="mcpHeaders"/> is false.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token,
        object? body = null, bool mcpHeaders = true)
    {
        using FinTrackClient client = CreateClient();
        using HttpRequestMessage request = new(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (mcpHeaders)
        {
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        HttpResponseMessage response =
            await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken);
        await response.Content.LoadIntoBufferAsync(CancellationToken);
        return response;
    }
}
#endif
