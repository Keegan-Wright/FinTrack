using System.Net;
using System.Net.Http.Headers;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Tokens;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Email;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// Making, listing and revoking personal access tokens through the API, as the API tokens page does.
/// </summary>
public class TokensApiTests : IntegrationTestBase
{
    private const string TokenName = "Placeholder script";

    [Test]
    public async Task Create_WithThePassword_GivesATokenThatReadsAndEmailsTheUser()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ITokensService tokens = ApiFor(user).Get<ITokensService>();

        // Act
        CreatedToken? created = await tokens.CreateAsync(Create(SignedInUser.DefaultPassword), CancellationToken);

        // Assert
        HttpResponseMessage read = await ReadWithAsync(created!.Token!);
        IReadOnlyList<TokenSummary>? listed = await tokens.GetTokensAsync(CancellationToken);
        IReadOnlyList<CaughtMail> mail =
            await App.Mail.WaitForAsync(EmailOf(user), AccountEmails.TokenCreatedSubject, CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(created.Token).StartsWith("ftk_");
            await Assert.That(read.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(listed!.Single().Name).IsEqualTo(TokenName);
            await Assert.That(listed!.Single().ExpiresAt).IsNotNull();
            await Assert.That(mail.Single().Text).Contains(TokenName);
        }
    }

    [Test]
    public async Task Create_WrongPassword_IsRefused()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ITokensService tokens = ApiFor(user).Get<ITokensService>();

        // Act
        CreatedToken? created = await tokens.CreateAsync(Create("not-the-password"), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(created!.Errors).IsEquivalentTo(["Your password isn't right"]);
            await Assert.That(created.Token).IsNull();
            await Assert.That(await tokens.GetTokensAsync(CancellationToken)).IsEmpty();
        }
    }

    [Test]
    public async Task Create_NeverExpiring_HasNoExpiry()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ITokensService tokens = ApiFor(user).Get<ITokensService>();
        CreateToken request = Create(SignedInUser.DefaultPassword);
        request.Expiry = TokenExpiry.Never;

        // Act
        await tokens.CreateAsync(request, CancellationToken);

        // Assert
        await Assert.That((await tokens.GetTokensAsync(CancellationToken))!.Single().ExpiresAt).IsNull();
    }

    [Test]
    public async Task Revoke_StopsItStraightAwayAndEmailsTheUser()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ITokensService tokens = ApiFor(user).Get<ITokensService>();
        CreatedToken? created = await tokens.CreateAsync(Create(SignedInUser.DefaultPassword), CancellationToken);
        Guid id = (await tokens.GetTokensAsync(CancellationToken))!.Single().Id;

        // Act
        bool revoked = await tokens.RevokeAsync(id, CancellationToken);

        // Assert
        HttpResponseMessage read = await ReadWithAsync(created!.Token!);
        IReadOnlyList<CaughtMail> mail =
            await App.Mail.WaitForAsync(EmailOf(user), AccountEmails.TokenRevokedSubject, CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(revoked).IsTrue();
            await Assert.That(read.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That(await tokens.GetTokensAsync(CancellationToken)).IsEmpty();
            await Assert.That(mail.Single().Text).Contains(TokenName);
        }
    }

    [Test]
    public async Task Revoke_SomeoneElsesToken_IsNotFoundAndLeavesIt()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        CreatedToken? created = await ApiFor(owner).Get<ITokensService>()
            .CreateAsync(Create(SignedInUser.DefaultPassword), CancellationToken);
        Guid id = (await ApiFor(owner).Get<ITokensService>().GetTokensAsync(CancellationToken))!.Single().Id;

        // Act
        bool revoked = await ApiFor(other).Get<ITokensService>().RevokeAsync(id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(revoked).IsFalse();
            await Assert.That((await ReadWithAsync(created!.Token!)).StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    [Test]
    public async Task Tokens_ForAnAccountWithoutAPassword_AreNotOffered()
    {
        // Arrange, as one made by signing in through the identity provider
        SignedInUser user = await RegisterUserAsync();
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            await context.Users.Where(x => x.Id == user.UserId)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.PasswordHash, (string?)null), CancellationToken);
        }

        // Act
        ITokensService tokens = ApiFor(user).Get<ITokensService>();
        IReadOnlyList<TokenSummary>? listed = await tokens.GetTokensAsync(CancellationToken);
        CreatedToken? created = await tokens.CreateAsync(Create(SignedInUser.DefaultPassword), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(listed).IsNull();
            await Assert.That(created).IsNull();
        }
    }

    [Test]
    public async Task Tokens_WithAToken_CantBeListedOrMade()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        CreatedToken? created = await ApiFor(user).Get<ITokensService>()
            .CreateAsync(Create(SignedInUser.DefaultPassword), CancellationToken);

        // Act
        HttpResponseMessage response = await ReadWithAsync(created!.Token!, ApiRoutes.Tokens);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A token named <see cref="TokenName"/> lasting 90 days, with <paramref name="password"/>.
    /// </summary>
    private static CreateToken Create(string password) =>
        new() { Name = TokenName, Expiry = TokenExpiry.NinetyDays, Password = password };

    /// <summary>
    /// Reads <paramref name="path"/> with only <paramref name="token"/>, as a script would.
    /// </summary>
    private async Task<HttpResponseMessage> ReadWithAsync(string token, string path = $"{ApiRoutes.Calendar}/events")
    {
        using FinTrackClient client = CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response =
            await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken);
        await response.Content.LoadIntoBufferAsync(CancellationToken);
        return response;
    }

    /// <summary>
    /// The confirmed email <see cref="IntegrationTestBase.RegisterUserAsync"/> gave <paramref name="user"/>.
    /// </summary>
    private static string EmailOf(SignedInUser user) => $"{user.Username}@fintrack.test";
}
