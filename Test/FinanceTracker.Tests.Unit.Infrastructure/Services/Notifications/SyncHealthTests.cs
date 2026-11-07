using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class SyncHealthTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";
    private const string Connection = "bank";

    private readonly INotifier _notifier = Substitute.For<INotifier>();
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Before(Test)]
    public async Task SetUpAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection(Connection, Username));

    [Test]
    public async Task Failed_ForTheFirstTime_TellsThemItCouldntSync()
    {
        // Arrange
        string name = await NameAsync();

        // Act
        await Subject(Username, Now).RecordFailedAsync(ConnectionId, CancellationToken);

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.SyncFailed, Arg.Any<string>(),
            $"{name} couldn't sync", Arg.Any<string>(), ConsentNotices.RenewLink, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Failed_AgainOnTheNextRun_SaysNothingMore()
    {
        // Arrange
        await Subject(Username, Now).RecordFailedAsync(ConnectionId, CancellationToken);

        // Act
        await Subject(Username, Now.AddHours(4)).RecordFailedAsync(ConnectionId, CancellationToken);

        // Assert
        await _notifier.ReceivedWithAnyArgs(1).NotifyAsync(default, default!, default!, default!, default, default);
    }

    [Test]
    public async Task Failed_AfterSyncingAgain_TellsThemAgain()
    {
        // Arrange
        await Subject(Username, Now).RecordFailedAsync(ConnectionId, CancellationToken);
        await Subject(Username, Now.AddHours(4)).RecordSucceededAsync(ConnectionId, CancellationToken);

        // Act
        await Subject(Username, Now.AddHours(8)).RecordFailedAsync(ConnectionId, CancellationToken);

        // Assert, a new event, keyed by when it started failing again
        List<string> keys = _notifier.ReceivedCalls().Select(static x => (string)x.GetArguments()[1]!).ToList();
        using (Assert.Multiple())
        {
            await Assert.That(keys).Count().IsEqualTo(2);
            await Assert.That(keys.Distinct()).Count().IsEqualTo(2);
        }

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That((await context.BankConnections.SingleAsync(CancellationToken)).SyncFailingSince)
            .IsEqualTo(Now.AddHours(8));
    }

    [Test]
    public async Task Failed_SomeoneElsesConnection_ChangesAndSaysNothing()
    {
        // Act
        await Subject(OtherUsername, Now).RecordFailedAsync(ConnectionId, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
                default);
            await Assert.That((await context.BankConnections.SingleAsync(CancellationToken)).SyncFailingSince)
                .IsNull();
        }
    }

    private Guid ConnectionId => Scenario.ConnectionMappings[Connection];

    private SyncHealth Subject(string username, DateTime now) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SyncHealth>.Instance,
            new FixedTimeProvider(now), _notifier);

    private async Task<string> NameAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return (await context.BankConnections.SingleAsync(CancellationToken)).Name;
    }
}
