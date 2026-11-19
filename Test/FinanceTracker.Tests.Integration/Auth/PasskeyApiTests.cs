using System.Net;
using System.Text.Json;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// The passkeys API's rules, and what the browser is asked for. Making and using a passkey is in
/// <c>PasskeyFlowTests</c>, as it needs a browser.
/// </summary>
public class PasskeyApiTests : IntegrationTestBase
{
    [Test]
    public async Task StartAdding_WithThePassword_AsksForAPasskeyThatNeedsAFingerprintFaceOrPin()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        PasskeyChange? change = await ApiFor(user).Get<IPasskeyService>()
            .StartAddingAsync(new ConfirmPassword { Password = SignedInUser.DefaultPassword }, CancellationToken);

        // Assert, the domain is the public address's, and no algorithm hashes with SHA-1
        using JsonDocument options = JsonDocument.Parse(change!.CreationOptionsJson!);
        JsonElement root = options.RootElement;
        List<int> algorithms = root.GetProperty("pubKeyCredParams").EnumerateArray()
            .Select(static x => x.GetProperty("alg").GetInt32())
            .ToList();
        using (Assert.Multiple())
        {
            await Assert.That(root.GetProperty("rp").GetProperty("id").GetString())
                .IsEqualTo(App.WebBaseAddress.Host);
            await Assert.That(root.GetProperty("authenticatorSelection").GetProperty("userVerification").GetString())
                .IsEqualTo("required");
            await Assert.That(root.GetProperty("authenticatorSelection").GetProperty("residentKey").GetString())
                .IsEqualTo("required");
            await Assert.That(algorithms).Contains(-7);
            await Assert.That(algorithms).DoesNotContain(-65535);
        }
    }

    [Test]
    public async Task StartAdding_WrongPassword_IsRefused()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        PasskeyChange? change = await ApiFor(user).Get<IPasskeyService>()
            .StartAddingAsync(new ConfirmPassword { Password = "not-the-password" }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(change!.Errors).IsEquivalentTo(["Your password isn't right"]);
            await Assert.That(change.CreationOptionsJson).IsNull();
        }
    }

    [Test]
    public async Task Add_WithoutStartingFirst_IsRefused()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        PasskeyChange? change = await ApiFor(user).Get<IPasskeyService>()
            .AddAsync(new AddPasskey { CredentialJson = "{}", Name = "Placeholder" }, CancellationToken);

        // Assert
        await Assert.That(change!.Succeeded).IsFalse();
    }

    [Test]
    public async Task Passkeys_ForAnAccountWithoutAPassword_AreNotOffered()
    {
        // Arrange, as one made by signing in through the identity provider
        SignedInUser user = await RegisterUserAsync();
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            await context.Users.Where(x => x.Id == user.UserId)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.PasswordHash, (string?)null), CancellationToken);
        }

        // Act
        IPasskeyService passkeys = ApiFor(user).Get<IPasskeyService>();
        IReadOnlyList<PasskeySummary>? listed = await passkeys.GetPasskeysAsync(CancellationToken);
        PasskeyChange? started = await passkeys.StartAddingAsync(
            new ConfirmPassword { Password = SignedInUser.DefaultPassword }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(listed).IsNull();
            await Assert.That(started).IsNull();
        }
    }

    [Test]
    public async Task SignInOptions_WithoutTheSignInPagesToken_AreRefused()
    {
        // Arrange, as another site posting from the user's browser would
        using FinTrackClient client = CreateClient();

        // Act
        using HttpRequestMessage request = new(HttpMethod.Post, "/Account/PasskeyRequestOptions");
        HttpResponseMessage response =
            await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
