using System.Net;
using FinanceTracker.Client;
using FinanceTracker.Client.Api;
using FinanceTracker.Contracts.Api;

namespace FinanceTracker.Tests.Unit.Client.Api;

public class ServerVersionHandlerTests
{
    private const string Running = "0.1.1+3e871e2";

    [Test]
    [Arguments(HttpStatusCode.OK)]
    [Arguments(HttpStatusCode.BadRequest)]
    [Arguments(HttpStatusCode.InternalServerError)]
    public async Task Send_ServerRunsAnotherBuild_SaysAnUpdateIsAvailable(HttpStatusCode status)
    {
        // Arrange
        AppUpdate update = new(Running);
        using HttpClient client = ClientAnswering(update, status, "0.1.2+9a8b7c6");

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("api/bills", UriKind.Relative));

        // Assert, the answer passed on as it was
        using (Assert.Multiple())
        {
            await Assert.That(update.Available).IsTrue();
            await Assert.That(response.StatusCode).IsEqualTo(status);
        }
    }

    [Test]
    [Arguments(Running)]
    [Arguments(null)]
    public async Task Send_ServerRunsThisBuildOrDoesNotSay_NoUpdate(string? version)
    {
        // Arrange
        AppUpdate update = new(Running);
        using HttpClient client = ClientAnswering(update, HttpStatusCode.OK, version);

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("api/bills", UriKind.Relative));

        // Assert
        await Assert.That(update.Available).IsFalse();
    }

    private static HttpClient ClientAnswering(AppUpdate update, HttpStatusCode status, string? version)
    {
        AnsweringHandler server = new(status);
        if (version is not null)
        {
            server.Headers[ApiHeaders.Version] = version;
        }

        return new HttpClient(new ServerVersionHandler(update) { InnerHandler = server })
        {
            BaseAddress = new Uri("http://localhost/")
        };
    }
}
