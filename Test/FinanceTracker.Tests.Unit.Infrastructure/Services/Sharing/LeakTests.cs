using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Calendar;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Bills;
using FinanceTracker.Infrastructure.Budgets;
using FinanceTracker.Infrastructure.Calendar;
using FinanceTracker.Infrastructure.Classification;
using FinanceTracker.Infrastructure.Debts;
using FinanceTracker.Infrastructure.HouseholdMembers;
using FinanceTracker.Infrastructure.NetWorth;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Rules;
using FinanceTracker.Infrastructure.SavingsGoals;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Transactions;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Nothing of Sam's reaches anyone Sam hasn't shared it with. Sam has one of everything: an account with a payment
/// and a low balance, a tag, rule, bill, debt, budget, savings goal, calendar event and household member. Alex is
/// linked with Sam, Chris is linked with no one, and neither has anything of their own.
/// </summary>
public class LeakTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";
    private const string Chris = "chris";

    private readonly List<(Guid UserId, NotificationKind Kind, string Title)> _raised = [];
    private ServiceProvider _services = null!;
    private Guid _linkId;

    [Before(Test)]
    public async Task SeedAsync()
    {
        ServiceCollection services = new();
        services.AddCurrentUser();
        services.AddScoped<INotifier>(sp => new RecordingNotifier(sp.GetRequiredService<ICurrentUser>(), _raised));
        services.AddTransferMatching(DbContextFactory);
        _services = services.BuildServiceProvider();

        DateOnly tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await Scenario.Do(s => s.UserScenario.WithUser("Alex", "Smith", "alex@example.test", Alex))
            .Do(s => s.UserScenario.WithUser("Sam", "Taylor", "sam@example.test", Sam))
            .Do(s => s.UserScenario.WithUser("Chris", "Jones", "chris@example.test", Chris))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-current", "sam-bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "ext-current", "TRANSACTION", "Current", "GBP")))
            .Do(s => s.BankAccountScenario.WithBalance("sam-current", new AccountBalance(5m, 5m, DateTime.UtcNow)))
            .Do(s => s.TransactionScenario.WithTransaction("sofa", "sam-current", new TransactionDetailsFaker()
                .Generate() with
                {
                    Amount = -900m, Description = "SOFA SHOP", Type = TransactionType.Debit,
                    OccurredAt = DateTime.UtcNow.AddHours(-1), IsPending = false, LabelledAsTransfer = false
                }))
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Sam, "Groceries"))
            .Do(s => s.RuleScenario.WithRule("rule", Sam))
            .Do(s => s.BillScenario.WithBill("rent", Sam, "sam-current", (userId, accountId) =>
                new Bill(userId, accountId, "Rent", "LANDLORD", BillFrequency.Monthly, 800m, tomorrow,
                    BillSource.Manual)))
            .Do(s => s.DebtScenario.WithDebt("loan", Sam))
            .Do(s => s.BudgetScenario.WithBudget("food", Sam))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("holiday", Sam))
            .Do(s => s.CalendarEventScenario.WithEvent("birthday", Sam, userId =>
                new CalendarEvent(userId, "Mum's birthday", tomorrow, EventRecurrence.Yearly)))
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("partner", Sam));

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
        _raised.Clear();
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task LinkedButNothingShared_AlexSeesNothingOfSams_AndIsToldNothing()
    {
        await AssertSeesNothingAsync(Alex);
    }

    [Test]
    public async Task SamSharingEverythingWithAlex_ChrisStillSeesNothing()
    {
        // Arrange
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts =
                [new AccountShareChoice { AccountId = Id(Scenario.AccountMappings, "sam-current"), Mode = Change }]
        }, CancellationToken);
        await Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items =
            [
                Choice(SharedItemKind.Classification, Id(Scenario.ClassificationMappings, "groceries")),
                Choice(SharedItemKind.Rule, Id(Scenario.RuleMappings, "rule")),
                Choice(SharedItemKind.Bill, Id(Scenario.BillMappings, "rent")),
                Choice(SharedItemKind.Debt, Id(Scenario.DebtMappings, "loan")),
                Choice(SharedItemKind.Budget, Id(Scenario.BudgetMappings, "food")),
                Choice(SharedItemKind.SavingsGoal, Id(Scenario.SavingsGoalMappings, "holiday")),
                Choice(SharedItemKind.CalendarEvent, Id(Scenario.CalendarEventMappings, "birthday"))
            ]
        }, CancellationToken);
        _raised.Clear();

        // Act and assert
        await AssertSeesNothingAsync(Chris);

        // Alex, whom it's shared with, does see it and is told of it, so the checks above can find what leaks
        DateTime now = DateTime.UtcNow;
        using (Assert.Multiple())
        {
            await Assert.That(await Transactions(Alex).GetAllTransactionsAsync(new FilterTransactions(),
                CancellationToken).ToListAsync(CancellationToken)).IsNotEmpty();
            await Assert.That(await Classifications(Alex).GetAllCustomClassificationsAsync(CancellationToken)
                .ToListAsync(CancellationToken)).IsNotEmpty();
            await Assert.That(await RulesService(Alex).GetRulesAsync(CancellationToken)
                .ToListAsync(CancellationToken)).IsNotEmpty();
            await Assert.That(await Bills(Alex).GetBillsAsync(CancellationToken).ToListAsync(CancellationToken))
                .IsNotEmpty();
            await Assert.That(await Debts(Alex).GetDebtsAsync(CancellationToken).ToListAsync(CancellationToken))
                .IsNotEmpty();
            await Assert.That((await Budgets(Alex).GetMonthAsync(now.Year, now.Month, CancellationToken)).Budgets)
                .IsNotEmpty();
            await Assert.That(await Goals(Alex).GetSavingsGoalsAsync(CancellationToken)
                .ToListAsync(CancellationToken)).IsNotEmpty();
            await Assert.That(await Calendar(Alex).GetEventsAsync(CancellationToken)
                .ToListAsync(CancellationToken)).IsNotEmpty();
        }

        await RunSyncNoticesAsync(Alex);
        await Assert.That(_raised.Select(static x => x.Kind)).Contains(NotificationKind.BillDueSoon)
            .And.Contains(NotificationKind.LowBalance).And.Contains(NotificationKind.LargeTransaction);
    }

    /// <summary>
    /// Everything <paramref name="username"/> sees is empty, and their sync notices raise nothing.
    /// </summary>
    private async Task AssertSeesNothingAsync(string username)
    {
        DateTime now = DateTime.UtcNow;
        using (Assert.Multiple())
        {
            await Assert.That(await Transactions(username).GetAllTransactionsAsync(new FilterTransactions(),
                CancellationToken).ToListAsync(CancellationToken)).IsEmpty();
            await Assert.That(await Classifications(username).GetAllCustomClassificationsAsync(CancellationToken)
                .ToListAsync(CancellationToken)).IsEmpty();
            await Assert.That(await RulesService(username).GetRulesAsync(CancellationToken)
                .ToListAsync(CancellationToken)).IsEmpty();
            await Assert.That(await Bills(username).GetBillsAsync(CancellationToken).ToListAsync(CancellationToken))
                .IsEmpty();
            await Assert.That(await Debts(username).GetDebtsAsync(CancellationToken).ToListAsync(CancellationToken))
                .IsEmpty();
            await Assert.That((await Budgets(username).GetMonthAsync(now.Year, now.Month, CancellationToken))
                .Budgets).IsEmpty();
            await Assert.That(await Goals(username).GetSavingsGoalsAsync(CancellationToken)
                .ToListAsync(CancellationToken)).IsEmpty();
            await Assert.That(await Calendar(username).GetEventsAsync(CancellationToken)
                .ToListAsync(CancellationToken)).IsEmpty();
            await Assert.That(await Household(username).GetHouseholdMembersAsync(CancellationToken)
                .ToListAsync(CancellationToken)).IsEmpty();
            await Assert.That((await NetWorth(username).GetNetWorthAsync(null, CancellationToken)).Breakdown)
                .IsEmpty();
        }

        await RunSyncNoticesAsync(username);
        await Assert.That(_raised).IsEmpty();
    }

    /// <summary>
    /// Every notice a sync sends, as <paramref name="username"/>, watching for large payments and low balances on
    /// every account so any of Sam's they could see would be said.
    /// </summary>
    private async Task RunSyncNoticesAsync(string username)
    {
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            Domain.Notifications.NotificationSettings settings = new(Scenario.UserMappings[username]);
            settings.ChangeAlerts(Domain.Notifications.NotificationSettings.DefaultBudgetWarningPercent,
                Domain.Notifications.NotificationSettings.DefaultBillReminderDays, 1m);
            settings.WatchBalances(new Dictionary<Guid, decimal>
            {
                [Id(Scenario.AccountMappings, "sam-current")] = 1_000m
            });
            context.NotificationSettings.Add(settings);
            await context.SaveChangesAsync(CancellationToken);
        }

        RecordingNotifier notifier = new(CurrentUserFor(username), _raised);
        ISyncNotices[] notices =
        [
            new BillNotices(CurrentUserFor(username), DbContextFactory, NullLogger<BillNotices>.Instance,
                TimeProvider.System, notifier),
            new BudgetNotices(CurrentUserFor(username), DbContextFactory, NullLogger<BudgetNotices>.Instance,
                TimeProvider.System, Budgets(username), notifier),
            new BalanceNotices(CurrentUserFor(username), DbContextFactory, NullLogger<BalanceNotices>.Instance,
                TimeProvider.System, notifier),
            new TransactionNotices(CurrentUserFor(username), DbContextFactory,
                NullLogger<TransactionNotices>.Instance, TimeProvider.System, notifier)
        ];
        foreach (ISyncNotices notice in notices)
        {
            await notice.NotifyAsync(CancellationToken);
        }
    }

    private const ShareMode Change = ShareMode.AllowChanges;

    private static ItemShareChoice Choice(SharedItemKind kind, Guid id) =>
        new() { Kind = kind, ItemId = id, Mode = Change };

    private static Guid Id(Dictionary<string, Guid> mappings, string name) => mappings[name];

    private AccountScope Scope(string username) => new(CurrentUserFor(username));

    private TransferMatchingService Matching(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<TransferMatchingService>.Instance,
            TimeProvider.System, TestMetrics.Unobserved);

    private TransactionsService Transactions(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<TransactionsService>.Instance,
            TimeProvider.System, Scope(username));

    private ClassificationService Classifications(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<ClassificationService>.Instance, Scope(username),
            TimeProvider.System);

    private RulesService RulesService(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<RulesService>.Instance,
            new RuleApplicationService(CurrentUserFor(username), DbContextFactory,
                NullLogger<RuleApplicationService>.Instance, Matching(username), Scope(username),
                TimeProvider.System, TestMetrics.Unobserved),
            Scope(username), TimeProvider.System);

    private BillsService Bills(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<BillsService>.Instance,
            new BillSyncService(CurrentUserFor(username), DbContextFactory, NullLogger<BillSyncService>.Instance,
                TimeProvider.System, TestMetrics.Unobserved), TimeProvider.System, Scope(username));

    private DebtsService Debts(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<DebtsService>.Instance, TimeProvider.System,
            Scope(username));

    private BudgetsService Budgets(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<BudgetsService>.Instance, TimeProvider.System);

    private SavingsGoalsService Goals(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SavingsGoalsService>.Instance,
            TimeProvider.System);

    private CalendarService Calendar(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<CalendarService>.Instance, TimeProvider.System,
            Goals(username), Debts(username), Scope(username));

    private HouseholdMemberService Household(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<HouseholdMemberService>.Instance,
            Matching(username));

    private NetWorthService NetWorth(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<NetWorthService>.Instance, TimeProvider.System,
            Scope(username));

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
