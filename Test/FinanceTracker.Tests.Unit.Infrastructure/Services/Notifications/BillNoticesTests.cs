using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class BillNoticesTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string Account = "everyday";
    private const string Bill = "rent";

    /// <summary>
    /// Bills are stamped as added at the real time they're saved, so the test's today is well after that, as if they
    /// were added weeks ago.
    /// </summary>
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40);

    private readonly INotifier _notifier = Substitute.For<INotifier>();

    [Before(Test)]
    public async Task SetUpAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount(Account, "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-1", "TRANSACTION", "Everyday", "GBP")));

    [Test]
    public async Task Notify_UnpaidPastItsDueDate_SaysItHasntBeenPaid()
    {
        // Arrange, due a week ago and never paid
        using CultureScope culture = new();
        DateOnly due = Today.AddDays(-7);
        await WithBillAsync(due);

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.BillLate, LateKey(due), "Rent hasn't been paid",
            $"£800.00 was due from Everyday on {due:d MMMM}.", BillNotices.BillsLink, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_PaidADifferentAmount_SaysWhatItWas()
    {
        // Arrange
        using CultureScope culture = new();
        DateOnly due = Today.AddDays(-7);
        await WithBillAsync(due);
        await WithPaymentAsync(due, 850m);

        // Act
        await NotifyAsync();

        // Assert
        using (Assert.Multiple())
        {
            await _notifier.Received(1).NotifyAsync(NotificationKind.BillChanged,
                $"bill-changed:{BillId:N}:{due:yyyyMMdd}", "Rent was £850.00",
                $"£800.00 was expected for {due:d MMMM}.", BillNotices.BillsLink, Arg.Any<CancellationToken>());
            await _notifier.DidNotReceive().NotifyAsync(NotificationKind.BillLate, Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_PaidAsExpected_SaysNothingOfIt()
    {
        // Arrange
        DateOnly due = Today.AddDays(-7);
        await WithBillAsync(due);
        await WithPaymentAsync(due, 800m);

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.DidNotReceive().NotifyAsync(Arg.Is<NotificationKind>(x => x != NotificationKind.BillDueSoon),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_LateLongerAgoThanItLooksBack_SaysNothing()
    {
        // Arrange, a quarterly bill whose last due date is past the look back
        DateOnly due = Today.AddDays(-(BillNotices.LookBackDays + 5));
        await WithBillAsync(due, BillFrequency.Quarterly);

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    [Test]
    public async Task Notify_DueBeforeItWasAdded_SaysNothing()
    {
        // Arrange, today is when it was added, so the period a week ago is before then
        DateOnly addedToday = DateOnly.FromDateTime(DateTime.UtcNow);
        await WithBillAsync(addedToday.AddDays(-7));

        // Act
        await NotifyAsync(addedToday);

        // Assert
        await _notifier.DidNotReceive().NotifyAsync(NotificationKind.BillLate, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_DueWithinTheReminderDays_RemindsThem()
    {
        // Arrange, the balance covers it
        using CultureScope culture = new();
        DateOnly due = Today.AddDays(2);
        await WithBillAsync(due);
        await WithAvailableAsync(1000m);

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.BillDueSoon, $"bill-due:{BillId:N}:{due:yyyyMMdd}",
            "Rent is due in 2 days", $"£800.00 goes out from Everyday on {due:d MMMM}.", BillNotices.BillsLink,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_DueSoonButTheBalanceWontCoverIt_SaysSo()
    {
        // Arrange
        using CultureScope culture = new();
        DateOnly due = Today.AddDays(1);
        await WithBillAsync(due);
        await WithAvailableAsync(500m);

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.BillDueSoon, Arg.Any<string>(),
            "Rent is due tomorrow",
            $"£800.00 goes out from Everyday on {due:d MMMM}. Its available balance of £500.00 won't cover it.",
            BillNotices.BillsLink, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_DueAfterTheReminderDays_DoesntRemindThemYet()
    {
        // Arrange, reminded a day before, due in two
        await WithBillAsync(Today.AddDays(2));
        await WithSettingsAsync(x => x.ChangeAlerts(80, 1, null));

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    [Test]
    public async Task Notify_AlreadyPaidAheadOfItsDueDate_DoesntRemindThem()
    {
        // Arrange
        DateOnly due = Today.AddDays(2);
        await WithBillAsync(due);
        await WithPaymentAsync(Today, 800m);

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.DidNotReceive().NotifyAsync(NotificationKind.BillDueSoon, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private Guid BillId => Scenario.BillMappings[Bill];

    private string LateKey(DateOnly due) => $"bill-late:{BillId:N}:{due:yyyyMMdd}";

    /// <summary>
    /// £800 rent from Everyday, first due on <paramref name="due"/>, so no period before it gets in the way.
    /// </summary>
    private Task<TestingScenario> WithBillAsync(DateOnly due, BillFrequency frequency = BillFrequency.Monthly) =>
        Scenario.Do(s => s.BillScenario.WithBill(Bill, Username, Account, (userId, accountId) =>
            new Bill(userId, accountId, "Rent", "LANDLORD", frequency, 800m, due, BillSource.Manual)));

    private Task<TestingScenario> WithPaymentAsync(DateOnly on, decimal amount)
    {
        TransactionDetails payment = new TransactionDetailsFaker().Generate() with
        {
            Description = "LANDLORD",
            Type = TransactionType.Debit,
            Amount = -amount,
            OccurredAt = on.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
            IsPending = false
        };

        return Scenario.Do(s => s.TransactionScenario.WithTransaction("payment", Account, payment))
            .Do(s => s.TransactionScenario.WithBillPayment("payment", Bill));
    }

    private Task<TestingScenario> WithAvailableAsync(decimal available) =>
        Scenario.Do(s => s.BankAccountScenario.WithBalance(Account,
            new AccountBalance(available, available, DateTime.UtcNow)));

    private async Task WithSettingsAsync(Action<NotificationSettings> change)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = new(Scenario.UserMappings[Username]);
        change(settings);
        context.NotificationSettings.Add(settings);
        await context.SaveChangesAsync(CancellationToken);
    }

    private Task NotifyAsync() => NotifyAsync(Today);

    private Task NotifyAsync(DateOnly today) =>
        new BillNotices(CurrentUserFor(Username), DbContextFactory, NullLogger<BillNotices>.Instance,
                new FixedTimeProvider(today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), TimeZoneInfo.Utc),
                _notifier)
            .NotifyAsync(CancellationToken);
}
