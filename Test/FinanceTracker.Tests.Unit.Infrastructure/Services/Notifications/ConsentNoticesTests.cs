using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class ConsentNoticesTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";
    private const string Connection = "bank";

    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly INotifier _notifier = Substitute.For<INotifier>();

    [Before(Test)]
    public async Task SetUpAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection(Connection, Username));

    [Test]
    public async Task Notify_MoreThanAWeekLeft_SaysNothing()
    {
        // Arrange
        await EndsAtAsync(Now.AddDays(8));

        // Act
        await NotifyAsync(Username);

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    [Test]
    public async Task Notify_FiveDaysLeft_SaysHowManyAndWhenToRenewBy()
    {
        // Arrange
        await EndsAtAsync(Now.AddDays(5));
        string name = await NameAsync();

        // Act
        await NotifyAsync(Username);

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.ConsentEndingSoon,
            Arg.Is<string>(x => x.StartsWith("consent-ending-week:", StringComparison.Ordinal)),
            $"{name} access ends in 5 days", $"Renew access before 11 November to keep {name} syncing.",
            ConsentNotices.RenewLink, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_HoursLeft_SaysWithinADay()
    {
        // Arrange
        await EndsAtAsync(Now.AddHours(6));
        string name = await NameAsync();

        // Act
        await NotifyAsync(Username);

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.ConsentEndingSoon,
            Arg.Is<string>(x => x.StartsWith("consent-ending-day:", StringComparison.Ordinal)),
            $"{name} access ends within a day", Arg.Any<string>(), ConsentNotices.RenewLink,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_Ended_SaysItHasEnded()
    {
        // Arrange
        await EndsAtAsync(Now.AddDays(-2));
        string name = await NameAsync();

        // Act
        await NotifyAsync(Username);

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.ConsentEnded,
            Arg.Is<string>(x => x.StartsWith("consent-ended:", StringComparison.Ordinal)),
            $"{name} access has ended", Arg.Any<string>(), ConsentNotices.RenewLink, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_EndedLongerAgoThanReadNoticesAreKept_SaysNothing()
    {
        // Arrange, its notice could have been read and pruned already, so telling them again would repeat it
        await EndsAtAsync(Now - NotificationRetention.ReadKeptFor - TimeSpan.FromDays(1));

        // Act
        await NotifyAsync(Username);

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    [Test]
    public async Task Notify_RenewedSoItEndsLater_IsANewEvent()
    {
        // Arrange, warned for one consent, then renewed to one that ends later but is still in its last week
        await EndsAtAsync(Now.AddDays(5));
        await NotifyAsync(Username);
        string first = (string)_notifier.ReceivedCalls().Single().GetArguments()[1]!;
        _notifier.ClearReceivedCalls();
        await EndsAtAsync(Now.AddDays(6));

        // Act
        await NotifyAsync(Username);

        // Assert
        string second = (string)_notifier.ReceivedCalls().Single().GetArguments()[1]!;
        await Assert.That(second).IsNotEqualTo(first);
    }

    [Test]
    public async Task Notify_SomeoneElsesConnection_SaysNothing()
    {
        // Arrange
        await EndsAtAsync(Now.AddDays(2));

        // Act
        await NotifyAsync(OtherUsername);

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    private Task NotifyAsync(string username) =>
        new ConsentNotices(CurrentUserFor(username), DbContextFactory, NullLogger<ConsentNotices>.Instance,
            new FixedTimeProvider(Now, TimeZoneInfo.Utc), _notifier).NotifyAsync(CancellationToken);

    private Task<TestingScenario> EndsAtAsync(DateTime endsAt) =>
        Scenario.Do(s => s.BankConnectionScenario.WithConsentExpiresAt(Connection, endsAt));

    private async Task<string> NameAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return (await context.BankConnections.SingleAsync(CancellationToken)).Name;
    }
}
