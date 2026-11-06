using System.Net;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Api;

public class ApiSecurityTests : IntegrationTestBase
{
    [Test]
    public async Task Api_ForAnAnonymousCaller_AnswersUnauthorisedRatherThanRedirecting()
    {
        // Arrange
        using FinTrackClient client = CreateClient();
        using HttpRequestMessage request = ApiRequest(HttpMethod.Get, ApiRoutes.HouseholdMembers);

        // Act
        HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead,
            CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Api_WithoutTheRequestHeader_IsRejected()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        HttpResponseMessage response = await user.Client.GetAsync(ApiRoutes.HouseholdMembers, CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments(ApiRoutes.HouseholdMembers, true)]
    [Arguments(ApiRoutes.HouseholdMembers, false)]
    [Arguments("api/not-a-route", true)]
    public async Task Api_EveryAnswer_SaysWhichBuildTheServerRuns(string path, bool withRequestHeader)
    {
        // Arrange, answered, rejected, and not found
        SignedInUser user = await RegisterUserAsync();
        using HttpRequestMessage request = withRequestHeader
            ? ApiRequest(HttpMethod.Get, path)
            : new HttpRequestMessage(HttpMethod.Get, path);

        // Act
        HttpResponseMessage response = await user.Client.SendAsync(request, HttpCompletionOption.ResponseContentRead,
            CancellationToken);

        // Assert, the front end's build, the one the server serves
        response.Headers.TryGetValues(ApiHeaders.Version, out IEnumerable<string>? versions);
        await Assert.That(versions).IsEquivalentTo([FinanceTracker.Client.Layout.AppVersion.Full]);
    }

    [Test]
    public async Task Api_AnAddressThatIsntAnEndpoint_AnswersABare404RatherThanThePage()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        using HttpRequestMessage request = ApiRequest(HttpMethod.Get, "api/not-a-route");

        // Act
        HttpResponseMessage response = await user.Client.SendAsync(request, HttpCompletionOption.ResponseContentRead,
            CancellationToken);

        // Assert
        string body = await response.Content.ReadAsStringAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(body).DoesNotContain("<html", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Test]
    [Arguments(ApiRoutes.AutomationJobs)]
    [Arguments($"{ApiRoutes.AutomationJobs}/summaries")]
    [Arguments($"{ApiRoutes.AutomationRuns}?function=SyncAllOpenBankingDetailsAsync")]
    public async Task AutomationJobs_ForANonAdmin_AreForbidden(string path)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        using HttpRequestMessage request = ApiRequest(HttpMethod.Get, path);

        // Act
        HttpResponseMessage response = await user.Client.SendAsync(request, HttpCompletionOption.ResponseContentRead,
            CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task FirstRegisteredUser_IsAnAdmin()
    {
        // Arrange
        IAutomationService automation = ApiFor(App.FirstUser).Get<IAutomationService>();

        // Act
        List<AutomationCron> jobs = await automation.GetJobsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(jobs).IsNotEmpty();
    }

    [Test]
    public async Task RunNow_ForANonAdmin_IsForbidden()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        using HttpRequestMessage request = ApiRequest(HttpMethod.Post, $"{ApiRoutes.AutomationJobs}/{Guid.NewGuid()}/run");

        // Act
        HttpResponseMessage response = await user.Client.SendAsync(request, HttpCompletionOption.ResponseContentRead,
            CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    private static HttpRequestMessage ApiRequest(HttpMethod method, string path)
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Add(ApiHeaders.Request, "1");
        return request;
    }
}
