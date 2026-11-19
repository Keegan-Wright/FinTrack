using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account.Tokens;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// Reading data with a personal access token, as a script or Home Assistant would, and everything a token mustn't
/// do. Tokens are made straight in the database until the app can make them.
/// </summary>
public class PersonalAccessTokenTests : IntegrationTestBase
{
    private const string EventTitle = "Placeholder trip";

    [Test]
    public async Task Read_WithAToken_GetsOnlyItsUsersData()
    {
        // Arrange, an event for one user, and tokens for them and someone else
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        await AddEventAsync(owner);
        string ownersToken = await TokenForAsync(owner);
        string othersToken = await TokenForAsync(other);

        // Act, without the app's own request header, which a script won't send
        HttpResponseMessage owners = await SendAsync(HttpMethod.Get, $"{ApiRoutes.Calendar}/events", ownersToken);
        HttpResponseMessage others = await SendAsync(HttpMethod.Get, $"{ApiRoutes.Calendar}/events", othersToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(owners.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(await owners.Content.ReadAsStringAsync(CancellationToken)).Contains(EventTitle);
            await Assert.That(others.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(await others.Content.ReadAsStringAsync(CancellationToken)).DoesNotContain(EventTitle);
        }
    }

    [Test]
    public async Task Read_APostThatOnlyReads_Works()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string token = await TokenForAsync(user);

        // Act
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"{ApiRoutes.Transactions}/search", token,
            new FilterTransactions());

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Write_WithAToken_IsForbidden()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string token = await TokenForAsync(user);

        // Act
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"{ApiRoutes.Calendar}/events", token,
            new SaveCalendarEvent { Title = EventTitle, Date = new DateOnly(2026, 12, 1) });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
            await Assert.That(await ApiFor(user).Get<ICalendarService>().GetEventsAsync(CancellationToken)
                .ToListAsync(CancellationToken)).IsEmpty();
        }
    }

    [Test]
    [Arguments("api/profile")]
    [Arguments("api/profile/two-factor/authenticator")]
    [Arguments("api/profile/passkeys")]
    [Arguments("api/notifications/settings")]
    [Arguments("api/sharing/links")]
    [Arguments("api/exports/backup")]
    public async Task Read_AnAccountOrSettingsEndpoint_IsForbidden(string path)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string token = await TokenForAsync(user);

        // Act
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, path, token);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    [Arguments("api/users")]
    [Arguments("api/automation/jobs/summaries")]
    public async Task Read_AnAdminEndpointWithAnAdminsToken_IsForbidden(string path)
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        string token = await TokenForAsync(admin);

        // Act
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, path, token);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Read_NotSignedIn_IsUnauthorised()
    {
        // Arrange, the app's own request header but no cookie or token
        using FinTrackClient client = CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, $"{ApiRoutes.Calendar}/events");
        request.Headers.Add(ApiHeaders.Request, "1");

        // Act
        HttpResponseMessage response =
            await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Read_ExpiredToken_IsUnauthorised()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string token = await TokenForAsync(user, expiresAt: DateTime.UtcNow.AddMinutes(-1));

        // Act
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"{ApiRoutes.Calendar}/events", token);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Read_RevokedToken_IsUnauthorisedStraightAway()
    {
        // Arrange, used once, then revoked
        SignedInUser user = await RegisterUserAsync();
        string token = await TokenForAsync(user);
        HttpResponseMessage before = await SendAsync(HttpMethod.Get, $"{ApiRoutes.Calendar}/events", token);
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            PersonalAccessToken stored = await context.PersonalAccessTokens.SingleAsync(x => x.UserId == user.UserId,
                CancellationToken);
            stored.Revoke(DateTime.UtcNow);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        HttpResponseMessage after = await SendAsync(HttpMethod.Get, $"{ApiRoutes.Calendar}/events", token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(before.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(after.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }
    }

    [Test]
    public async Task Read_LockedUsersToken_IsUnauthorised()
    {
        // Arrange
        SignedInUser admin = await RegisterAdminAsync();
        SignedInUser user = await RegisterUserAsync();
        string token = await TokenForAsync(user);
        await ApiFor(admin).Get<IUsersService>().LockAsync(user.UserId, CancellationToken);

        // Act
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"{ApiRoutes.Calendar}/events", token);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Read_WrongSecretOrMadeUpToken_IsUnauthorisedWithoutARedirect()
    {
        // Arrange, a real token's id with another's secret, and a token in the format nobody issued
        SignedInUser user = await RegisterUserAsync();
        string token = await TokenForAsync(user);
        string otherSecret = PersonalAccessTokenFormat.Issue().Token.Split('_')[2];
        string wrongSecret = $"{token[..(token.LastIndexOf('_') + 1)]}{otherSecret}";
        string madeUp = PersonalAccessTokenFormat.Issue().Token;

        // Act
        HttpResponseMessage wrong = await SendAsync(HttpMethod.Get, $"{ApiRoutes.Calendar}/events", wrongSecret);
        HttpResponseMessage unknown = await SendAsync(HttpMethod.Get, $"{ApiRoutes.Calendar}/events", madeUp);
        HttpResponseMessage malformed = await SendAsync(HttpMethod.Get, $"{ApiRoutes.Calendar}/events", "ftk_nope");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(wrong.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That(malformed.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That(wrong.Headers.WwwAuthenticate.ToString()).IsEqualTo("Bearer");
        }
    }

    [Test]
    public async Task Page_WithAToken_IsNotSignedIn()
    {
        // Arrange, the app's pages aren't open to tokens
        SignedInUser user = await RegisterUserAsync();
        string token = await TokenForAsync(user);

        // Act
        HttpResponseMessage response = await SendAsync(HttpMethod.Get, "/YourProfile", token);

        // Assert
        await Assert.That(response.Headers.Location?.OriginalString).Contains("Account/Login");
    }

    [Test]
    public async Task Read_WithAToken_RecordsWhenItWasUsed()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        string token = await TokenForAsync(user);

        // Act
        await SendAsync(HttpMethod.Get, $"{ApiRoutes.Calendar}/events", token);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        PersonalAccessToken stored = await context.PersonalAccessTokens.SingleAsync(x => x.UserId == user.UserId,
            CancellationToken);
        await Assert.That(stored.LastUsedAt).IsNotNull();
    }

    [Test]
    public async Task Stored_SecretHashAndName_AreEncrypted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        await TokenForAsync(user);

        // Assert, the columns as they're stored, as {keyId}:{ciphertext}
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<string> stored = await context.Database
            .SqlQuery<string>($"""
                SELECT "SecretHash" AS "Value" FROM "PersonalAccessTokens" WHERE "UserId" = {user.UserId}
                UNION ALL
                SELECT "Name" AS "Value" FROM "PersonalAccessTokens" WHERE "UserId" = {user.UserId}
                """)
            .ToListAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(stored.Count).IsEqualTo(2);
            await Assert.That(stored).All().Satisfy(static x => x.StartsWith("1:", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// A new token for <paramref name="user"/>, made in the database as the app will.
    /// </summary>
    /// <returns>The whole token, as the user would be shown it.</returns>
    private async Task<string> TokenForAsync(SignedInUser user, DateTime? expiresAt = null)
    {
        IssuedToken issued = PersonalAccessTokenFormat.Issue();
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        context.PersonalAccessTokens.Add(new PersonalAccessToken(user.UserId, "Placeholder script", issued.LookupId,
            issued.SecretHash, expiresAt));
        await context.SaveChangesAsync(CancellationToken);
        return issued.Token;
    }

    /// <summary>
    /// Adds a calendar event for <paramref name="user"/> through the app.
    /// </summary>
    private async Task AddEventAsync(SignedInUser user) =>
        await ApiFor(user).Get<ICalendarService>().AddEventAsync(
            new SaveCalendarEvent { Title = EventTitle, Date = new DateOnly(2026, 12, 1) }, CancellationToken);

    /// <summary>
    /// Sends a request with only <paramref name="token"/>, no cookie and none of the app's headers.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token,
        object? body = null)
    {
        using FinTrackClient client = CreateClient();
        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
