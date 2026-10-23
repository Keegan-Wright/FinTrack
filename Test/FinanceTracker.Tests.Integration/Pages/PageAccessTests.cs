using System.Net;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Pages;

public class PageAccessTests : IntegrationTestBase
{
    public static IEnumerable<string> SignedInPages() =>
    [
        "/",
        "/Accounts",
        "/Calendar",
        "/Classifications",
        "/HouseholdMembers",
        "/ProviderSetup",
        "/Report/ByAccount",
        "/Report/ByCategory",
        "/Report/InTimePeriod",
        "/Transactions"
    ];

    [Test]
    [MethodDataSource(nameof(SignedInPages))]
    public async Task Page_RendersForASignedInUser(string path)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        HttpResponseMessage response = await user.Client.GetAsync(path, CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    [MethodDataSource(nameof(SignedInPages))]
    public async Task Page_RedirectsAnonymousVisitorsToLogin(string path)
    {
        // Arrange
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(path, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            await Assert.That(response.Headers.Location?.ToString()).Contains("Account/Login");
        }
    }

    [Test]
    public async Task AutomationPage_RendersForAnAdmin()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();

        // Act
        HttpResponseMessage response = await admin.Client.GetAsync("/Automation", CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task AutomationPage_SendsNonAdminsToAccessDenied()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        HttpResponseMessage response = await user.Client.GetAsync("/Automation", CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            await Assert.That(response.Headers.Location?.ToString()).Contains("Account/AccessDenied");
        }
    }

    [Test]
    [Arguments("/Account/Login")]
    [Arguments("/Account/Register")]
    public async Task AccountPage_IsAvailableAnonymously(string path)
    {
        // Arrange
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(path, CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
