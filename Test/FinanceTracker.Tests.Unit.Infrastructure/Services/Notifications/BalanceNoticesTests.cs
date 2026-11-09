using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class BalanceNoticesTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string Account = "everyday";

    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly INotifier _notifier = Substitute.For<INotifier>();

    [Before(Test)]
    public async Task SetUpAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount(Account, "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-1", "TRANSACTION", "Everyday", "GBP")));

    [Test]
    public async Task Notify_WatchedAccountBelowItsAmount_SaysSo()
    {
        // Arrange
        using CultureScope culture = new();
        await WatchAsync(100m);
        await WithAvailableAsync(42.5m);

        // Act
        await NotifyAsync(Now);

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.LowBalance,
            $"low-balance:{AccountId:N}:{Now:yyyyMMddHHmmss}", "Everyday is below £100.00",
            "Its available balance is £42.50.", BalanceNotices.AccountsLink, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_StillBelowOnTheNextSync_SaysNothingMore()
    {
        // Arrange
        await WatchAsync(100m);
        await WithAvailableAsync(42.5m);
        await NotifyAsync(Now);

        // Act
        await NotifyAsync(Now.AddHours(4));

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.LowBalance, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_BackAboveThenBelowAgain_SaysSoAgain()
    {
        // Arrange
        await WatchAsync(100m);
        await WithAvailableAsync(42.5m);
        await NotifyAsync(Now);
        await WithAvailableAsync(500m);
        await NotifyAsync(Now.AddDays(1));
        await WithAvailableAsync(10m);

        // Act
        await NotifyAsync(Now.AddDays(2));

        // Assert, a new key, so it's raised again
        using (Assert.Multiple())
        {
            await _notifier.Received(1).NotifyAsync(NotificationKind.LowBalance,
                $"low-balance:{AccountId:N}:{Now:yyyyMMddHHmmss}", Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>());
            await _notifier.Received(1).NotifyAsync(NotificationKind.LowBalance,
                $"low-balance:{AccountId:N}:{Now.AddDays(2):yyyyMMddHHmmss}", Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_AboveItsAmount_SaysNothingAndRemembersNothing()
    {
        // Arrange
        await WatchAsync(100m);
        await WithAvailableAsync(100m);

        // Act
        await NotifyAsync(Now);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = await context.NotificationSettings.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
                default);
            await Assert.That(settings.LowBalanceAlerts.Single().LowSince).IsNull();
        }
    }

    [Test]
    public async Task Notify_AccountNotWatched_SaysNothing()
    {
        // Arrange
        await WithAvailableAsync(-300m);

        // Act
        await NotifyAsync(Now);

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    private Guid AccountId => Scenario.AccountMappings[Account];

    private async Task WatchAsync(decimal below)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = new(Scenario.UserMappings[Username]);
        settings.WatchBalances(new Dictionary<Guid, decimal> { [AccountId] = below });
        context.NotificationSettings.Add(settings);
        await context.SaveChangesAsync(CancellationToken);
    }

    private Task<TestingScenario> WithAvailableAsync(decimal available) =>
        Scenario.Do(s => s.BankAccountScenario.WithBalance(Account, new AccountBalance(available, available, Now)));

    private Task NotifyAsync(DateTime now) =>
        new BalanceNotices(CurrentUserFor(Username), DbContextFactory, NullLogger<BalanceNotices>.Instance,
            new FixedTimeProvider(now, TimeZoneInfo.Utc), _notifier).NotifyAsync(CancellationToken);
}
