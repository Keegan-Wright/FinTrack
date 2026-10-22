using System.Net;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Auth;

public class AuthenticationTests : IntegrationTestBase
{
    [Test]
    public async Task Register_SignsTheNewUserIn()
    {
        // Act
        SignedInUser user = await RegisterUserAsync();

        // Assert
        await Assert.That(user.Client.IsSignedIn).IsTrue();
    }

    [Test]
    public async Task Register_WithAnExistingUsername_IsRejected()
    {
        // Arrange
        SignedInUser existing = await RegisterUserAsync();
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.RegisterAsync(existing.Username, SignedInUser.DefaultPassword, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(await response.Content.ReadAsStringAsync(CancellationToken)).Contains("is already taken");
            await Assert.That(client.IsSignedIn).IsFalse();
        }
    }

    [Test]
    public async Task Login_WithCorrectPassword_SignsIn()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            await Assert.That(client.IsSignedIn).IsTrue();
        }
    }

    [Test]
    public async Task Login_WithWrongPassword_IsRejected()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.LoginAsync(user.Username, "Wrong-Password-1!", CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(await response.Content.ReadAsStringAsync(CancellationToken)).Contains("Invalid login attempt.");
            await Assert.That(client.IsSignedIn).IsFalse();
        }
    }

    [Test]
    public async Task Login_AfterRepeatedFailures_LocksTheAccountOut()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        using FinTrackClient client = CreateClient();

        // Identity's default lockout threshold is five failed attempts
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await client.LoginAsync(user.Username, "Wrong-Password-1!", CancellationToken);
        }

        // Act
        HttpResponseMessage response = await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            await Assert.That(response.Headers.Location!.ToString()).Contains("Account/Lockout");
            await Assert.That(client.IsSignedIn).IsFalse();
        }
    }

    [Test]
    public async Task Logout_SignsTheUserOut()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        HttpResponseMessage logout = await user.Client.LogoutAsync(CancellationToken);
        HttpResponseMessage home = await user.Client.GetAsync("/", CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(logout.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            await Assert.That(user.Client.IsSignedIn).IsFalse();
            await Assert.That(home.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            await Assert.That(home.Headers.Location!.ToString()).Contains("Account/Login");
        }
    }

    [Test]
    public async Task OidcChallenge_IsNotMapped_WhenOidcIsNotConfigured()
    {
        // Arrange
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/auth/oidc", CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
