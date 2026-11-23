using System.Net;
using System.Net.Http.Headers;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Tokens;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// Someone who signs in through the identity provider making an API token. Instead of a password they sign in at
/// the provider again, which confirms it's them for one token.
/// </summary>
public class SingleSignOnTokenTests : IntegrationTestBase
{
    private const string ReturnUrl = "/ApiTokens?new=true";

    [Test]
    public async Task Confirm_SignsInAtTheProviderAgainThenMakesOneToken()
    {
        // Arrange
        (SignedInUser user, FakeOidcUser person) = await SignInThroughProviderAsync();
        ITokensService tokens = ApiFor(user).Get<ITokensService>();
        TokensOverview before = await tokens.GetTokensAsync(CancellationToken);

        // Act
        Dictionary<string, StringValues> authorize = await ChallengeConfirmAsync(user);
        HttpResponseMessage returned =
            await SingleSignOnSteps.FinishAsync(user.Client, App.Oidc, authorize, person, CancellationToken);
        TokensOverview after = await tokens.GetTokensAsync(CancellationToken);
        TokenCreation? first = await tokens.CreateAsync(Create(), CancellationToken);
        TokenCreation? second = await tokens.CreateAsync(Create(), CancellationToken);

        // Assert, the provider was asked to sign them in again, without max_age=0, which Authelia can never satisfy,
        // and the confirmation made one token only
        using (Assert.Multiple())
        {
            await Assert.That(before.Confirmation).IsEqualTo(TokenConfirmation.SingleSignOn);
            await Assert.That(before.Confirmed).IsFalse();
            await Assert.That(authorize["prompt"].ToString()).IsEqualTo("login");
            await Assert.That(authorize.ContainsKey("max_age")).IsFalse();
            await Assert.That(returned.Headers.Location?.OriginalString).IsEqualTo(ReturnUrl);
            await Assert.That(after.Confirmed).IsTrue();
            await Assert.That(((TokenCreated)first!).Token).StartsWith("ftk_");
            await Assert.That(await ReadWithAsync(((TokenCreated)first).Token)).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(second).IsTypeOf<TokenCreationRefused>();
        }
    }

    [Test]
    public async Task Create_WithoutConfirming_IsRefused()
    {
        // Arrange
        (SignedInUser user, _) = await SignInThroughProviderAsync();

        // Act
        TokenCreation? created = await ApiFor(user).Get<ITokensService>().CreateAsync(Create(), CancellationToken);

        // Assert
        TokenCreationRefused refused = (TokenCreationRefused)created!;
        await Assert.That(refused.Errors.Single()).Contains("Confirm it's you with single sign-on first");
    }

    [Test]
    public async Task Confirm_ProviderUsedAnEarlierSignIn_DoesNotConfirm()
    {
        // Arrange, the provider answers with its session from an hour ago rather than a fresh sign in
        (SignedInUser user, FakeOidcUser person) = await SignInThroughProviderAsync();

        // Act
        Dictionary<string, StringValues> authorize = await ChallengeConfirmAsync(user);
        HttpResponseMessage returned = await SingleSignOnSteps.FinishAsync(user.Client, App.Oidc, authorize, person,
            CancellationToken, DateTimeOffset.UtcNow.AddHours(-1));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(returned.Headers.Location?.OriginalString).Contains("sso=failed");
            await Assert.That((await ApiFor(user).Get<ITokensService>().GetTokensAsync(CancellationToken)).Confirmed)
                .IsFalse();
        }
    }

    [Test]
    public async Task Confirm_SomeoneElseSignsInAtTheProvider_DoesNotConfirm()
    {
        // Arrange
        (SignedInUser user, _) = await SignInThroughProviderAsync();

        // Act
        Dictionary<string, StringValues> authorize = await ChallengeConfirmAsync(user);
        HttpResponseMessage returned = await SingleSignOnSteps.FinishAsync(user.Client, App.Oidc, authorize,
            NewPerson(), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(returned.Headers.Location?.OriginalString).Contains("sso=failed");
            await Assert.That((await ApiFor(user).Get<ITokensService>().GetTokensAsync(CancellationToken)).Confirmed)
                .IsFalse();
        }
    }

    [Test]
    public async Task Confirm_PasswordUser_IsSentBackWithoutGoingToTheProvider()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        HttpResponseMessage response = await user.Client.GetAsync(
            $"/auth/oidc/confirm?returnUrl={Uri.EscapeDataString(ReturnUrl)}", CancellationToken);

        // Assert
        await Assert.That(response.Headers.Location?.OriginalString).IsEqualTo("/ApiTokens?new=true&sso=failed");
    }

    /// <summary>
    /// Someone new signing in through the provider, which makes their account.
    /// </summary>
    private async Task<(SignedInUser User, FakeOidcUser Person)> SignInThroughProviderAsync()
    {
        FakeOidcUser person = NewPerson();
        FinTrackClient client = CreateClient();
        Dictionary<string, StringValues> authorize =
            await SingleSignOnSteps.ChallengeAsync(client, "/auth/oidc?returnUrl=%2F", CancellationToken);
        await SingleSignOnSteps.FinishAsync(client, App.Oidc, authorize, person, CancellationToken);

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        FinanceTrackerUser user = await context.Users.AsNoTracking()
            .SingleAsync(x => x.OpenIdConnectSubject == person.Subject, CancellationToken);
        return (new SignedInUser(user.UserName!, user.Id, client), person);
    }

    /// <summary>
    /// The API tokens page's New token for a single sign-on user, which sends them to the provider.
    /// </summary>
    private Task<Dictionary<string, StringValues>> ChallengeConfirmAsync(SignedInUser user) =>
        SingleSignOnSteps.ChallengeAsync(user.Client,
            $"/auth/oidc/confirm?returnUrl={Uri.EscapeDataString(ReturnUrl)}", CancellationToken);

    private static FakeOidcUser NewPerson()
    {
        string username = UniqueName("sso");
        return new FakeOidcUser(Guid.NewGuid().ToString(), $"{username}@sso.fintrack.test", true, username);
    }

    private static CreateToken Create() => new() { Name = "Placeholder script", Expiry = TokenExpiry.NinetyDays };

    /// <summary>
    /// Reads with only <paramref name="token"/>, as a script would.
    /// </summary>
    private async Task<HttpStatusCode> ReadWithAsync(string token)
    {
        using FinTrackClient client = CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, $"{ApiRoutes.Calendar}/events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response =
            await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken);
        return response.StatusCode;
    }
}
