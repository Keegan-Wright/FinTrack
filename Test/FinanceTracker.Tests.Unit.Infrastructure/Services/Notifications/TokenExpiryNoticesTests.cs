using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class TokenExpiryNoticesTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";
    private const string TokenName = "Home Assistant";

    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly INotifier _notifier = Substitute.For<INotifier>();

    [Before(Test)]
    public async Task SetUpAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername));

    [Test]
    public async Task Notify_MoreThanAWeekLeft_SaysNothing()
    {
        // Arrange
        await AddTokenAsync(Username, Now.AddDays(8));

        // Act
        await NotifyAsync(Username);

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    [Test]
    public async Task Notify_FiveDaysLeft_SaysHowManyAndWhenToReplaceItBy()
    {
        // Arrange
        await AddTokenAsync(Username, Now.AddDays(5));

        // Act
        await NotifyAsync(Username);

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.ApiTokenExpiring,
            Arg.Is<string>(x => x.StartsWith("token-expiring:", StringComparison.Ordinal)),
            $"API token {TokenName} expires in 5 days",
            $"Make a new token before 11 November and swap it into whatever uses {TokenName}.",
            TokenExpiryNotices.TokensLink, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_Expired_SaysItHasExpired()
    {
        // Arrange
        await AddTokenAsync(Username, Now.AddDays(-2));

        // Act
        await NotifyAsync(Username);

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.ApiTokenExpiring,
            Arg.Is<string>(x => x.StartsWith("token-expired:", StringComparison.Ordinal)),
            $"API token {TokenName} has expired", Arg.Any<string>(), TokenExpiryNotices.TokensLink,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_NeverExpiringOrRevokedOrSomeoneElses_SaysNothing()
    {
        // Arrange
        await AddTokenAsync(Username, null);
        await AddTokenAsync(Username, Now.AddDays(2), revoked: true);
        await AddTokenAsync(OtherUsername, Now.AddDays(2));

        // Act
        await NotifyAsync(Username);

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    [Test]
    public async Task Notify_ExpiredLongerAgoThanReadNotificationsAreKept_SaysNothing()
    {
        // Arrange, so pruning its notice doesn't raise it again
        await AddTokenAsync(Username, Now - NotificationRetention.ReadKeptFor - TimeSpan.FromDays(1));

        // Act
        await NotifyAsync(Username);

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    private Task NotifyAsync(string username) =>
        new TokenExpiryNotices(CurrentUserFor(username), DbContextFactory, NullLogger<TokenExpiryNotices>.Instance,
            new FixedTimeProvider(Now, TimeZoneInfo.Utc), _notifier).NotifyAsync(CancellationToken);

    /// <summary>
    /// A token named <see cref="TokenName"/> for <paramref name="username"/>, expiring at <paramref name="expiresAt"/>.
    /// </summary>
    private async Task AddTokenAsync(string username, DateTime? expiresAt, bool revoked = false)
    {
        PersonalAccessToken token = new(Scenario.UserMappings[username], TokenName,
            Guid.NewGuid().ToString("N")[..16], [1, 2, 3], expiresAt);
        if (revoked)
        {
            token.Revoke(Now.AddDays(-1));
        }

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        context.PersonalAccessTokens.Add(token);
        await context.SaveChangesAsync(CancellationToken);
    }
}
