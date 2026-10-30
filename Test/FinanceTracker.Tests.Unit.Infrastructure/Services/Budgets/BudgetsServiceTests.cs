using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure.Budgets;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using Budget = FinanceTracker.Contracts.Budgets.Budget;
using DomainBudget = FinanceTracker.Domain.Budgeting.Budget;
using DomainRolloverMode = FinanceTracker.Domain.Budgeting.RolloverMode;
using RolloverMode = FinanceTracker.Contracts.Budgets.RolloverMode;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Budgets;

public class BudgetsServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    /// <summary>
    /// Midday mid-March, far enough from a month's edge that the machine's time zone doesn't matter.
    /// </summary>
    private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    private static readonly BudgetMonth January = new(2026, 1);
    private static readonly BudgetMonth February = new(2026, 2);
    private static readonly BudgetMonth March = new(2026, 3);

    [Test]
    public async Task AddBudgetAsync_RunsFromThisMonth()
    {
        // Arrange
        await SeedAsync(Username);

        // Act
        Budget added = await BuildSubject(Username).AddBudgetAsync(Groceries(400m), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.MonthlyLimit).IsEqualTo(400m);
            await Assert.That(added.Limits.Single().From).IsEqualTo(March.FirstDay);
            await Assert.That(added.RolloverMode).IsEqualTo(RolloverMode.CarryUnderspend);
        }
    }

    [Test]
    public async Task AddBudgetAsync_AnotherUsersTag_Throws()
    {
        // Arrange
        await SeedAsync(Username);
        await SeedAsync(OtherUsername, "theirs");
        SaveBudget budget = Groceries(400m);
        budget.TagIds = [Scenario.ClassificationMappings["theirs"]];

        // Act & Assert
        await Assert.That(async () => await BuildSubject(Username).AddBudgetAsync(budget, CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UpdateBudgetAsync_NewLimit_AppliesFromThisMonthOnly()
    {
        // Arrange
        await SeedAsync(Username)
            .Do(s => s.BudgetScenario.WithBudget("groceries", Username, userId => DomainGroceries(userId, January)));
        BudgetsService subject = BuildSubject(Username);

        // Act
        Budget updated = await subject.UpdateBudgetAsync(Scenario.BudgetMappings["groceries"], Groceries(500m),
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(updated.MonthlyLimit).IsEqualTo(500m);
            await Assert.That(updated.Limits.Select(static x => (x.From, x.Amount))).IsEquivalentTo(
                [(January.FirstDay, 400m), (March.FirstDay, 500m)],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task GetMonthAsync_ThisMonth_ShowsSpendingAndWhatNoBudgetCovers()
    {
        // Arrange
        await SeedAsync(Username)
            .Do(s => s.BudgetScenario.WithBudget("groceries", Username, userId => DomainGroceries(userId, March)))
            .Do(s => s.TransactionScenario.WithTransaction("shop", "main",
                Spend(-30m, new DateTime(2026, 3, 5, 12, 0, 0))))
            .Do(s => s.TransactionScenario.WithCustomClassification("shop", "shopping"))
            .Do(s => s.TransactionScenario.WithTransaction("cafe", "main",
                Spend(-4.5m, new DateTime(2026, 3, 6, 12, 0, 0))))
            .Do(s => s.TransactionScenario.WithTransaction("pay", "main",
                Spend(2000m, new DateTime(2026, 3, 1, 12, 0, 0))));

        // Act
        BudgetMonthOverview overview = await BuildSubject(Username).GetMonthAsync(2026, 3, CancellationToken);

        // Assert
        BudgetStatus groceries = overview.Budgets.Single();
        using (Assert.Multiple())
        {
            await Assert.That(overview.IsCurrent).IsTrue();
            await Assert.That(groceries.Spent).IsEqualTo(30m);
            await Assert.That(groceries.Remaining).IsEqualTo(370m);
            await Assert.That(overview.Unbudgeted).IsEqualTo(4.5m);
        }
    }

    [Test]
    public async Task GetMonthAsync_PastMonth_UsesItsOwnLimitAndCarriesOver()
    {
        // Arrange, £100 of £400 spent in January, the limit raised to £500 from February
        await SeedAsync(Username)
            .Do(s => s.BudgetScenario.WithBudget("groceries", Username, userId =>
            {
                DomainBudget budget = DomainGroceries(userId, January);
                budget.ChangeLimit(500m, February);
                return budget;
            }))
            .Do(s => s.TransactionScenario.WithTransaction("shop", "main",
                Spend(-100m, new DateTime(2026, 1, 10, 12, 0, 0))))
            .Do(s => s.TransactionScenario.WithCustomClassification("shop", "shopping"));

        // Act
        BudgetMonthOverview overview = await BuildSubject(Username).GetMonthAsync(2026, 2, CancellationToken);

        // Assert
        BudgetStatus groceries = overview.Budgets.Single();
        using (Assert.Multiple())
        {
            await Assert.That(overview.IsCurrent).IsFalse();
            await Assert.That(groceries.Limit).IsEqualTo(500m);
            await Assert.That(groceries.CarriedIn).IsEqualTo(300m);
            await Assert.That(groceries.Available).IsEqualTo(800m);
        }
    }

    [Test]
    public async Task GetMonthAsync_BeforeABudgetStarted_LeavesItOut()
    {
        // Arrange
        await SeedAsync(Username)
            .Do(s => s.BudgetScenario.WithBudget("groceries", Username, userId => DomainGroceries(userId, March)));

        // Act
        BudgetMonthOverview overview = await BuildSubject(Username).GetMonthAsync(2026, 2, CancellationToken);

        // Assert
        await Assert.That(overview.Budgets).IsEmpty();
    }

    [Test]
    public async Task GetBudgetsAsync_OnlyTheUsers_ByName()
    {
        // Arrange
        await SeedAsync(Username)
            .Do(s => s.BudgetScenario.WithBudget("travel", Username, userId => Named(userId, "Travel")))
            .Do(s => s.BudgetScenario.WithBudget("food", Username, userId => Named(userId, "Food")));
        await SeedAsync(OtherUsername, "theirs")
            .Do(s => s.BudgetScenario.WithBudget("theirs", OtherUsername));

        // Act
        List<Budget> budgets = await BuildSubject(Username).GetBudgetsAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(budgets.Select(static x => x.Name)).IsEquivalentTo(["Food", "Travel"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task DeleteBudgetAsync_AnotherUsersBudget_Throws()
    {
        // Arrange
        await SeedAsync(Username);
        await SeedAsync(OtherUsername, "theirs")
            .Do(s => s.BudgetScenario.WithBudget("theirs", OtherUsername));

        // Act & Assert
        await Assert.That(async () => await BuildSubject(Username)
                .DeleteBudgetAsync(Scenario.BudgetMappings["theirs"], CancellationToken))
            .Throws<InvalidOperationException>();
    }

    private Task<TestingScenario> SeedAsync(string username, string tagName = "shopping") =>
        Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection($"{username}-bank", username))
            .Do(s => s.BankAccountScenario.WithAccount(username == Username ? "main" : $"{username}-main",
                $"{username}-bank"))
            .Do(s => s.ClassificationScenario.WithClassification(tagName, username, "Shopping"));

    private SaveBudget Groceries(decimal limit) => new()
    {
        Name = "Groceries",
        TagIds = [Scenario.ClassificationMappings["shopping"]],
        MonthlyLimit = limit,
        RolloverMode = RolloverMode.CarryUnderspend
    };

    private DomainBudget DomainGroceries(Guid userId, BudgetMonth from) =>
        new(userId, "Groceries", [Scenario.ClassificationMappings["shopping"]], 400m, from,
            DomainRolloverMode.CarryUnderspend);

    private static DomainBudget Named(Guid userId, string name)
    {
        DomainBudget budget = new BudgetFaker(userId).Generate();
        budget.Rename(name);
        return budget;
    }

    private static TransactionDetails Spend(decimal amount, DateTime occurredAt) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            OccurredAt = DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc),
            IsPending = false,
            LabelledAsTransfer = false
        };

    private BudgetsService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<BudgetsService>.Instance,
            new FixedTimeProvider(Now));
}
