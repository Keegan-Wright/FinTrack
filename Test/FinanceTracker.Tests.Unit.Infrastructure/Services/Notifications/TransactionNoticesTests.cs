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

public class TransactionNoticesTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string Account = "everyday";

    /// <summary>
    /// The real time, as the database narrows encrypted dates from it.
    /// </summary>
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly INotifier _notifier = Substitute.For<INotifier>();

    [Before(Test)]
    public async Task SetUpAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount(Account, "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-1", "TRANSACTION", "Everyday", "GBP")));

    [Test]
    public async Task Notify_PaymentOverTheirAmount_SaysWhoItWasPaidTo()
    {
        // Arrange
        using CultureScope culture = new();
        await GivenLargeOverAsync(500m);
        await WithPaymentAsync("sofa shop", -650m, Now.AddDays(-2));

        // Act
        await NotifyAsync();

        // Assert
        DateOnly paidOn = DateOnly.FromDateTime(Now.AddDays(-2));
        await _notifier.Received(1).NotifyAsync(NotificationKind.LargeTransaction,
            $"large-transaction:{Scenario.TransactionMappings["sofa shop"]:N}", "£650.00 paid to SOFA SHOP",
            $"Paid from Everyday on {paidOn:d MMMM}.", TransactionNotices.TransactionsLink,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_ExactlyTheirAmount_SaysIt()
    {
        // Arrange
        await GivenLargeOverAsync(500m);
        await WithPaymentAsync("sofa", -500m, Now.AddDays(-1));

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.LargeTransaction, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_ThingsThatArentLargeSpending_SaysNothing()
    {
        // Arrange
        await GivenLargeOverAsync(500m);
        await WithPaymentAsync("small", -499m, Now.AddDays(-1));
        await WithPaymentAsync("pay", 2000m, Now.AddDays(-1));
        await WithPaymentAsync("pending", -900m, Now.AddDays(-1), pending: true);
        await WithPaymentAsync("old", -900m, Now.AddDays(-(TransactionNotices.LookBackDays + 2)));
        await WithPaymentAsync("to savings", -900m, Now.AddDays(-1));
        await Scenario.Do(s => s.TransactionScenario.WithTransfer("to savings"));

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    [Test]
    public async Task Notify_NoAmountSet_SaysNothing()
    {
        // Arrange
        await WithPaymentAsync("sofa", -5000m, Now.AddDays(-1));

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    private async Task GivenLargeOverAsync(decimal over)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = new(Scenario.UserMappings[Username]);
        settings.ChangeAlerts(NotificationSettings.DefaultBudgetWarningPercent,
            NotificationSettings.DefaultBillReminderDays, over);
        context.NotificationSettings.Add(settings);
        await context.SaveChangesAsync(CancellationToken);
    }

    /// <param name="name">Its name in the scenario, and in capitals what the bank calls it.</param>
    /// <param name="amount">Negative for money out.</param>
    private Task<TestingScenario> WithPaymentAsync(string name, decimal amount, DateTime occurredAt,
        bool pending = false)
    {
        TransactionType type = TransactionType.Credit;
        if (amount < 0)
        {
            type = TransactionType.Debit;
        }

        TransactionDetails details = new TransactionDetailsFaker().Generate() with
        {
            Description = name.ToUpperInvariant(),
            Type = type,
            Amount = amount,
            OccurredAt = occurredAt,
            IsPending = pending
        };

        return Scenario.Do(s => s.TransactionScenario.WithTransaction(name, Account, details));
    }

    private Task NotifyAsync() =>
        new TransactionNotices(CurrentUserFor(Username), DbContextFactory, NullLogger<TransactionNotices>.Instance,
            new FixedTimeProvider(Now, TimeZoneInfo.Utc), _notifier).NotifyAsync(CancellationToken);
}
