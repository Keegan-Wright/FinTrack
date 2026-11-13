using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Budgets;
using FinanceTracker.Infrastructure.Calendar;
using FinanceTracker.Infrastructure.Classification;
using FinanceTracker.Infrastructure.Debts;
using FinanceTracker.Infrastructure.HouseholdMembers;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.SavingsGoals;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ContractRolloverMode = FinanceTracker.Contracts.Budgets.RolloverMode;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Sam's budgets, savings goals, calendar events and household seen by Alex. Sam spends £40 on groceries from their
/// own account and has a Holiday pot holding £300, which Sam shares with Alex. Alex shares Joint with Sam, and spends
/// £25 on groceries there.
/// </summary>
public class PlanningSharingTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

    private static readonly DateTime Spent = DateTime.UtcNow.AddHours(-1);

    private ServiceProvider _services = null!;
    private Guid _linkId;

    [Before(Test)]
    public async Task SeedAsync()
    {
        List<(Guid UserId, NotificationKind Kind, string Title)> raised = [];
        ServiceCollection services = new();
        services.AddCurrentUser();
        services.AddScoped<INotifier>(sp => new RecordingNotifier(sp.GetRequiredService<ICurrentUser>(), raised));
        services.AddTransferMatching(DbContextFactory);
        _services = services.BuildServiceProvider();

        await Scenario.Do(s => s.UserScenario.WithUser("Alex", "Smith", "alex@example.test", Alex))
            .Do(s => s.UserScenario.WithUser("Sam", "Taylor", "sam@example.test", Sam))
            .Do(s => s.BankConnectionScenario.WithConnection("alex-bank", Alex))
            .Do(s => s.BankAccountScenario.WithAccount("alex-joint", "alex-bank", Named("Joint")))
            .Do(s => s.TransactionScenario.WithTransaction("alex-shop", "alex-joint", Details(-25m, "TESCO")))
            .Do(s => s.ClassificationScenario.WithClassification("alex-food", Alex, "Food"))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-current", "sam-bank", Named("Current")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-pot", "sam-bank", Named("Holiday pot")))
            .Do(s => s.BankAccountScenario.WithBalance("sam-pot", new AccountBalance(300m, 300m, DateTime.UtcNow)))
            .Do(s => s.TransactionScenario.WithTransaction("sam-shop", "sam-current", Details(-40m, "ALDI")))
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Sam, "Groceries"));

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;

        await Sharing(Alex).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("alex-joint"), Mode = ShareMode.AllowChanges }]
        }, CancellationToken);
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-pot"), Mode = ShareMode.ReadOnly }]
        }, CancellationToken);
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task ABudget_CountsOnlyItsOwnersSpending_UntilShared_ThenTheHouseholds_TheSameForBoth()
    {
        // Arrange, Sam's groceries budget, and Sam's tag on Alex's shop on Joint, which Sam can change
        Budget budget = await Budgets(Sam).AddBudgetAsync(new SaveBudget
        {
            Name = "Groceries", TagIds = [Tag("groceries")], MonthlyLimit = 400m,
            RolloverMode = ContractRolloverMode.None
        }, CancellationToken);
        await TagAsync(Sam, "sam-shop", "groceries");
        await TagAsync(Sam, "alex-shop", "groceries");
        decimal before = (await ThisMonthAsync(Sam)).Budgets.Single().Spent;

        // Act
        await ShareAsync(SharedItemKind.Budget, budget.Id, ShareMode.ReadOnly);

        // Assert
        BudgetStatus asSam = (await ThisMonthAsync(Sam)).Budgets.Single();
        BudgetStatus asAlex = (await ThisMonthAsync(Alex)).Budgets.Single();
        using (Assert.Multiple())
        {
            await Assert.That(before).IsEqualTo(40m);
            await Assert.That((asSam.Spent, asSam.IsHousehold, asSam.SharedBy)).IsEqualTo((65m, true, (string?)null));
            await Assert.That((asAlex.Spent, asAlex.IsHousehold, asAlex.SharedBy, asAlex.CanChange))
                .IsEqualTo((65m, true, (string?)"Sam Taylor", false));
        }
    }

    [Test]
    public async Task ASharedBudget_ChangesAllowed_AlexEditsIt_OnlyWithSamsTagsSharedWithThem()
    {
        // Arrange
        Budget budget = await Budgets(Sam).AddBudgetAsync(new SaveBudget
        {
            Name = "Groceries", TagIds = [Tag("groceries")], MonthlyLimit = 400m
        }, CancellationToken);
        await ShareAsync(SharedItemKind.Budget, budget.Id, ShareMode.AllowChanges);

        // Act
        Budget saved = await Budgets(Alex).UpdateBudgetAsync(budget.Id, new SaveBudget
        {
            Name = "Food shop", TagIds = [Tag("groceries")], MonthlyLimit = 450m
        }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((saved.Name, saved.SharedBy, saved.TagNames[Tag("groceries")]))
                .IsEqualTo(("Food shop", (string?)"Sam Taylor", "Groceries"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Budgets(Alex).UpdateBudgetAsync(budget.Id,
                new SaveBudget { Name = "Food shop", TagIds = [Tag("alex-food")], MonthlyLimit = 450m },
                CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Budgets(Alex).DeleteBudgetAsync(budget.Id, CancellationToken));
        }
    }

    [Test]
    public async Task AGoalTrackingASharedAccount_GoesWithIt_AndAlexCantChangeWhatItTracks()
    {
        // Arrange
        SavingsGoal goal = await Goals(Sam).AddSavingsGoalAsync(new SaveSavingsGoal
        {
            Name = "Holiday", Target = 1_000m, Source = SavingsGoalSource.Account, AccountId = Account("sam-pot")
        }, CancellationToken);
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-pot"), Mode = ShareMode.AllowChanges }]
        }, CancellationToken);

        // Act
        List<SavingsGoal> alexs = await Goals(Alex).GetSavingsGoalsAsync(CancellationToken)
            .ToListAsync(CancellationToken);
        SavingsGoal renamed = await Goals(Alex).UpdateSavingsGoalAsync(goal.Id, new SaveSavingsGoal
        {
            Name = "Summer", Target = 1_200m, Source = SavingsGoalSource.Account, AccountId = Account("sam-pot")
        }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(alexs.Select(static x => (x.Name, x.Saved, x.SharedBy, x.AccountName)))
                .IsEquivalentTo([("Holiday", 300m, (string?)"Sam Taylor", (string?)"Holiday pot")]);
            await Assert.That((renamed.Name, renamed.Target)).IsEqualTo(("Summer", 1_200m));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Goals(Alex).UpdateSavingsGoalAsync(goal.Id,
                new SaveSavingsGoal { Name = "Summer", Target = 1_200m, Source = SavingsGoalSource.Manual },
                CancellationToken));
            await Assert.That(await Goals(Alex).DeleteSavingsGoalAsync(goal.Id, CancellationToken)).IsFalse();
        }
    }

    [Test]
    public async Task AnEvent_SharedReadOnly_ShowsForAlex_ButAlexCantChangeIt()
    {
        // Arrange
        CalendarEvent birthday = await Calendar(Sam).AddEventAsync(new SaveCalendarEvent
        {
            Title = "Mum's birthday", Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20),
            Recurrence = EventRecurrence.Yearly
        }, CancellationToken);

        // Act
        await ShareAsync(SharedItemKind.CalendarEvent, birthday.Id, ShareMode.ReadOnly);

        // Assert
        List<CalendarEvent> alexs = await Calendar(Alex).GetEventsAsync(CancellationToken)
            .ToListAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(alexs.Select(static x => (x.Title, x.SharedBy, x.CanChange)))
                .IsEquivalentTo([("Mum's birthday", (string?)"Sam Taylor", false)]);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Calendar(Alex).UpdateEventAsync(birthday.Id,
                new SaveCalendarEvent
                {
                    Title = "Mum", Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20),
                    Recurrence = EventRecurrence.Yearly
                }, CancellationToken));
        }
    }

    [Test]
    public async Task AHouseholdMember_LinkedToSam_TakesTheirName_AndEndingTheLinkKeepsThemUnlinked()
    {
        // Arrange
        HouseholdMember partner = await Household(Alex).AddHouseholdMemberAsync(new AddHouseholdMember
        {
            FirstName = "My", LastName = "Partner", YearlyIncome = 30_000m
        }, CancellationToken);

        // Act
        HouseholdMember linked = await Household(Alex).LinkHouseholdMemberAsync(partner.Id,
            new LinkHouseholdMember { LinkId = _linkId }, CancellationToken);
        await Sharing(Sam).EndAsync(_linkId, CancellationToken);

        // Assert
        HouseholdMember after = (await Household(Alex).GetHouseholdMembersAsync(CancellationToken)
            .ToListAsync(CancellationToken)).Single();
        using (Assert.Multiple())
        {
            await Assert.That((linked.FirstName, linked.LastName, linked.LinkedUsername, linked.YearlyIncome))
                .IsEqualTo(("Sam", "Taylor", (string?)Sam, (decimal?)30_000m));
            await Assert.That((after.FirstName, after.LinkedUserId)).IsEqualTo(("Sam", (Guid?)null));
        }
    }

    [Test]
    public async Task AHouseholdMember_CantBeLinkedThroughALinkThatIsntTheirs()
    {
        HouseholdMember partner = await Household(Alex).AddHouseholdMemberAsync(new AddHouseholdMember
        {
            FirstName = "My", LastName = "Partner", YearlyIncome = 0m
        }, CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Household(Alex).LinkHouseholdMemberAsync(
            partner.Id, new LinkHouseholdMember { LinkId = Guid.NewGuid() }, CancellationToken));
    }

    private Task<LinkShares> ShareAsync(SharedItemKind kind, Guid id, ShareMode mode) =>
        Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items = [new ItemShareChoice { Kind = kind, ItemId = id, Mode = mode }]
        }, CancellationToken);

    private Task TagAsync(string username, string transaction, string tag) =>
        Classifications(username).AddCustomClassificationsToTransactionAsync(new AddCustomClassificationsToTransaction
        {
            TransactionId = Scenario.TransactionMappings[transaction],
            Classifications = [new SelectedCustomClassification { ClassificationId = Tag(tag) }]
        }, CancellationToken);

    private Task<BudgetMonthOverview> ThisMonthAsync(string username)
    {
        DateTime now = DateTime.UtcNow;
        return Budgets(username).GetMonthAsync(now.Year, now.Month, CancellationToken);
    }

    private static Func<Guid, Guid, BankAccount> Named(string name) => (userId, connectionId) =>
        new BankAccount(userId, connectionId, $"ext-{name}", "TRANSACTION", name, "GBP");

    private static TransactionDetails Details(decimal amount, string description) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Description = description,
            Type = TransactionType.Debit,
            Currency = "GBP",
            OccurredAt = Spent,
            IsPending = false,
            LabelledAsTransfer = false,
            Metadata = TransactionMetadata.None
        };

    private Guid Account(string name) => Scenario.AccountMappings[name];

    private Guid Tag(string name) => Scenario.ClassificationMappings[name];

    private AccountScope Scope(string username) => new(CurrentUserFor(username));

    private BudgetsService Budgets(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<BudgetsService>.Instance, TimeProvider.System);

    private SavingsGoalsService Goals(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SavingsGoalsService>.Instance,
            TimeProvider.System);

    private CalendarService Calendar(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<CalendarService>.Instance, TimeProvider.System,
            Goals(username),
            new DebtsService(CurrentUserFor(username), DbContextFactory, NullLogger<DebtsService>.Instance,
                TimeProvider.System, Scope(username)),
            Scope(username));

    private HouseholdMemberService Household(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<HouseholdMemberService>.Instance,
            new TransferMatchingService(CurrentUserFor(username), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System, TestMetrics.Unobserved));

    private ClassificationService Classifications(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<ClassificationService>.Instance, Scope(username),
            TimeProvider.System);

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}
