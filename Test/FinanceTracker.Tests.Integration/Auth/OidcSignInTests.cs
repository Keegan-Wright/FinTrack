using FinanceTracker.Contracts.Auth;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// Signing in through the OIDC provider end to end: the app's challenge, the provider's answer posted back to the
/// handler, its token and user info fetched from <see cref="FakeOidcProvider"/>, and the local user signed in.
/// </summary>
public class OidcSignInTests : IntegrationTestBase
{
    private const string ReturnUrl = "/Bills";
    private const string SignInFailed = "/Account/Login?sso=failed";

    [Test]
    public async Task SignIn_SomeoneNew_CreatesTheirAccountAndSignsThemIn()
    {
        // Arrange
        using FinTrackClient client = CreateClient();
        FakeOidcUser person = NewPerson(emailVerified: true);

        // Act
        HttpResponseMessage response = await SignInAtProviderAsync(client, person);

        // Assert
        FinanceTrackerUser created = await UserWithEmailAsync(person.Email);
        using (Assert.Multiple())
        {
            await Assert.That(response.Headers.Location?.ToString()).IsEqualTo(ReturnUrl);
            await Assert.That(client.IsSignedIn).IsTrue();
            await Assert.That(created.OpenIdConnectSubject).IsEqualTo(person.Subject);
            await Assert.That(created.UserName).IsEqualTo(person.PreferredUsername);
            await Assert.That(created.EmailConfirmed).IsTrue();
        }
    }

    [Test]
    public async Task SignIn_SomeoneWithAnAccountByTheSameEmail_LinksItAndSignsThemIn()
    {
        // Arrange
        SignedInUser existing = await RegisterUserAsync();
        using FinTrackClient client = CreateClient();
        FakeOidcUser person = NewPerson(emailVerified: true) with { Email = $"{existing.Username}@fintrack.test" };

        // Act
        HttpResponseMessage response = await SignInAtProviderAsync(client, person);

        // Assert
        FinanceTrackerUser linked = await UserWithEmailAsync(person.Email);
        using (Assert.Multiple())
        {
            await Assert.That(response.Headers.Location?.ToString()).IsEqualTo(ReturnUrl);
            await Assert.That(client.IsSignedIn).IsTrue();
            await Assert.That(linked.Id).IsEqualTo(existing.UserId);
            await Assert.That(linked.OpenIdConnectSubject).IsEqualTo(person.Subject);
        }
    }

    [Test]
    public async Task SignIn_SomeoneLinkedBefore_SignsThemInAgain()
    {
        // Arrange
        FakeOidcUser person = NewPerson(emailVerified: true);
        using (FinTrackClient first = CreateClient())
        {
            await SignInAtProviderAsync(first, person);
        }

        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await SignInAtProviderAsync(client, person);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.Headers.Location?.ToString()).IsEqualTo(ReturnUrl);
            await Assert.That(client.IsSignedIn).IsTrue();
            await Assert.That(await UsersWithEmailCountAsync(person.Email)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task SignIn_ProviderHasNotCheckedTheEmail_DoesNotSignThemIn()
    {
        // Arrange, an unchecked email could be someone else's, so it can't match or create an account
        using FinTrackClient client = CreateClient();
        FakeOidcUser person = NewPerson(emailVerified: false);

        // Act
        HttpResponseMessage response = await SignInAtProviderAsync(client, person);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.Headers.Location?.ToString()).IsEqualTo(SignInFailed);
            await Assert.That(client.IsSignedIn).IsFalse();
            await Assert.That(await UsersWithEmailCountAsync(person.Email)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task SignIn_TheyDeclineAtTheProvider_SaysSingleSignOnFailed()
    {
        // Arrange
        using FinTrackClient client = CreateClient();
        Dictionary<string, StringValues> authorize = await ChallengeAsync(client);

        // Act, the provider sends them back with an error rather than a code
        HttpResponseMessage response = await PostToHandlerAsync(client, authorize, new Dictionary<string, string>
        {
            ["error"] = "access_denied",
            ["state"] = authorize["state"].ToString()
        });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.Headers.Location?.ToString()).IsEqualTo(SignInFailed);
            await Assert.That(client.IsSignedIn).IsFalse();
        }
    }

    [Test]
    public async Task SignIn_InTheAdminsGroup_AsksForGroupsAndMakesThemAnAdmin()
    {
        // Arrange
        using FinTrackClient client = CreateClient();
        FakeOidcUser person = NewPerson(emailVerified: true) with { Groups = ["family", "admins"] };

        // Act
        Dictionary<string, StringValues> authorize = await ChallengeAsync(client);
        await FinishSignInAsync(client, authorize, person);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(authorize["scope"].ToString().Split(' ')).Contains("groups");
            await Assert.That(await IsAdminAsync(person.Email)).IsTrue();
        }
    }

    [Test]
    public async Task SignIn_NoLongerInTheAdminsGroup_TakesTheirAdminRole()
    {
        // Arrange, an admin through the group at their first sign in
        FakeOidcUser person = NewPerson(emailVerified: true) with { Groups = ["admins"] };
        using (FinTrackClient first = CreateClient())
        {
            await SignInAtProviderAsync(first, person);
        }

        bool adminBefore = await IsAdminAsync(person.Email);
        using FinTrackClient client = CreateClient();

        // Act
        await SignInAtProviderAsync(client, person with { Groups = ["family"] });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(adminBefore).IsTrue();
            await Assert.That(await IsAdminAsync(person.Email)).IsFalse();
        }
    }

    [Test]
    public async Task SignIn_SomeoneElseNotInTheAdminsGroup_LeavesAPasswordAdminBe()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        using FinTrackClient client = CreateClient();

        // Act
        await SignInAtProviderAsync(client, NewPerson(emailVerified: true) with { Groups = ["family"] });

        // Assert
        await Assert.That(await IsAdminAsync($"{admin.Username}@fintrack.test")).IsTrue();
    }

    private static FakeOidcUser NewPerson(bool emailVerified)
    {
        string username = UniqueName("sso");
        return new FakeOidcUser(Guid.NewGuid().ToString(), $"{username}@sso.fintrack.test", emailVerified, username);
    }

    /// <summary>
    /// What the browser does: follows the sign in page's single sign on link, signs in and agrees at the provider,
    /// which posts back to the app, then follows the app on to finish signing in.
    /// </summary>
    /// <returns>Where the app sends them once it's done, <see cref="ReturnUrl"/> or the sign in page.</returns>
    private async Task<HttpResponseMessage> SignInAtProviderAsync(FinTrackClient client, FakeOidcUser person) =>
        await FinishSignInAsync(client, await ChallengeAsync(client), person);

    /// <summary>
    /// The rest of <see cref="SignInAtProviderAsync"/> once the app has sent them to the provider.
    /// </summary>
    /// <param name="authorize">The provider authorisation request's parameters, from <see cref="ChallengeAsync"/>.
    /// </param>
    private async Task<HttpResponseMessage> FinishSignInAsync(FinTrackClient client,
        Dictionary<string, StringValues> authorize, FakeOidcUser person)
    {
        string code = Guid.NewGuid().ToString("N");
        App.Oidc.Approve(code, authorize["nonce"].ToString(), person);

        HttpResponseMessage returned = await PostToHandlerAsync(client, authorize, new Dictionary<string, string>
        {
            ["code"] = code,
            ["state"] = authorize["state"].ToString()
        });

        // A failure the handler catches goes straight to the sign in page, success goes on to finish signing in
        string next = returned.Headers.Location?.ToString() ?? string.Empty;
        if (!next.StartsWith("/auth/oidc/complete", StringComparison.Ordinal))
        {
            return returned;
        }

        return await client.GetAsync(next, CancellationToken);
    }

    /// <summary>
    /// The sign in page's single sign on link, which sends them to the provider with what it must send back.
    /// </summary>
    /// <returns>The provider authorisation request's parameters, such as its state and nonce.</returns>
    private async Task<Dictionary<string, StringValues>> ChallengeAsync(FinTrackClient client)
    {
        HttpResponseMessage challenge =
            await client.GetAsync($"/auth/oidc?returnUrl={Uri.EscapeDataString(ReturnUrl)}", CancellationToken);

        Uri provider = challenge.Headers.Location
                       ?? throw new InvalidOperationException($"/auth/oidc answered {challenge.StatusCode}");
        return QueryHelpers.ParseQuery(provider.Query);
    }

    /// <summary>
    /// The provider sending them back to the app's OIDC handler, as a form post.
    /// </summary>
    private async Task<HttpResponseMessage> PostToHandlerAsync(FinTrackClient client,
        Dictionary<string, StringValues> authorize, Dictionary<string, string> answer)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, authorize["redirect_uri"].ToString())
        {
            Content = new FormUrlEncodedContent(answer)
        };
        return await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken);
    }

    private async Task<FinanceTrackerUser> UserWithEmailAsync(string email)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.Users.AsNoTracking().SingleAsync(x => x.Email == email, CancellationToken);
    }

    private async Task<bool> IsAdminAsync(string email)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid userId = await context.Users.Where(x => x.Email == email).Select(static x => x.Id)
            .SingleAsync(CancellationToken);
        return await context.UserRoles
            .Where(x => x.UserId == userId)
            .Join(context.Roles, static x => x.RoleId, static x => x.Id, static (_, role) => role.Name)
            .AnyAsync(static x => x == FinTrackRoles.Admin, CancellationToken);
    }

    private async Task<int> UsersWithEmailCountAsync(string email)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.Users.CountAsync(x => x.Email == email, CancellationToken);
    }
}
