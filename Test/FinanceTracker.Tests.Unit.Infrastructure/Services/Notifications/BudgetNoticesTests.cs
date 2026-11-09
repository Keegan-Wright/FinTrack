using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Budgets;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

public class BudgetNoticesTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string Account = "everyday";

    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly INotifier _notifier = Substitute.For<INotifier>();
    private int _spent;

    /// <summary>
    /// A £100 a month groceries budget from this month, no pay picked up so its months are calendar months.
    /// </summary>
    [Before(Test)]
    public async Task SetUpAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount(Account, "bank"))
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Username, "Groceries"))
            .Do(s => s.BudgetScenario.WithBudget("groceries", Username, userId =>
                new Budget(userId, "Groceries", [Scenario.ClassificationMappings["groceries"]], 100m,
                    new BudgetMonth(2026, 11), RolloverMode.None)));

    [Test]
    public async Task Notify_ReachedTheWarningPercent_SaysWhatsLeft()
    {
        // Arrange
        using CultureScope culture = new();
        await SpendAsync(85m);

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.Received(1).NotifyAsync(NotificationKind.BudgetNearLimit, $"budget-near:{BudgetId:N}:202611",
            "Groceries has £15.00 left", "£85.00 of £100.00 spent this month.", BudgetNotices.BudgetsLink,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Notify_Over_SaysByHowMuchAndNotThatItsNear()
    {
        // Arrange
        using CultureScope culture = new();
        await SpendAsync(120m);

        // Act
        await NotifyAsync();

        // Assert
        using (Assert.Multiple())
        {
            await _notifier.Received(1).NotifyAsync(NotificationKind.BudgetOverLimit,
                $"budget-over:{BudgetId:N}:202611", "Groceries is £20.00 over", "£120.00 of £100.00 spent this month.",
                BudgetNotices.BudgetsLink, Arg.Any<CancellationToken>());
            await _notifier.DidNotReceive().NotifyAsync(NotificationKind.BudgetNearLimit, Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task Notify_BelowTheWarningPercent_SaysNothing()
    {
        // Arrange
        await SpendAsync(50m);

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    [Test]
    public async Task Notify_TheirOwnWarningPercent_WarnsThere()
    {
        // Arrange, 85% spent but they're warned at 90%
        await SpendAsync(85m);
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            NotificationSettings settings = new(Scenario.UserMappings[Username]);
            settings.ChangeAlerts(90, NotificationSettings.DefaultBillReminderDays, null);
            context.NotificationSettings.Add(settings);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        await NotifyAsync();

        // Assert
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default!, default!, default,
            default);
    }

    private Guid BudgetId => Scenario.BudgetMappings["groceries"];

    /// <summary>
    /// Spends <paramref name="amount"/> on groceries earlier this month.
    /// </summary>
    private async Task SpendAsync(decimal amount)
    {
        string name = $"shop-{_spent++}";
        TransactionDetails shop = new TransactionDetailsFaker().Generate() with
        {
            Type = TransactionType.Debit,
            Category = "PURCHASE",
            Amount = -amount,
            OccurredAt = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc),
            IsPending = false
        };

        await Scenario.Do(s => s.TransactionScenario.WithTransaction(name, Account, shop))
            .Do(s => s.TransactionScenario.WithCustomClassification(name, "groceries"));
    }

    private Task NotifyAsync()
    {
        FixedTimeProvider clock = new(Now, TimeZoneInfo.Utc);
        BudgetsService budgets = new(CurrentUserFor(Username), DbContextFactory,
            NullLogger<BudgetsService>.Instance, clock);

        return new BudgetNotices(CurrentUserFor(Username), DbContextFactory, NullLogger<BudgetNotices>.Instance,
            clock, budgets, _notifier).NotifyAsync(CancellationToken);
    }
}
