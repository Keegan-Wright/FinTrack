using System.Net;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Api;

public class BudgetApiTests : IntegrationTestBase
{
    [Test]
    public async Task AddBudget_CanBeReadShownForTheMonthUpdatedAndDeleted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ApiSession api = ApiFor(user);
        IBudgetsService service = api.Get<IBudgetsService>();
        Classification shopping = await api.Get<IClassificationService>()
            .AddCustomClassificationAsync(new AddClassifications { Tag = "Shopping" }, CancellationToken);
        DateTime now = DateTime.Now;

        // Act
        Budget added = await service.AddBudgetAsync(Groceries(shopping, 400m), CancellationToken);
        Budget read = await service.GetBudgetAsync(added.Id, CancellationToken);
        BudgetMonthOverview month = await service.GetMonthAsync(now.Year, now.Month, CancellationToken);
        Budget updated = await service.UpdateBudgetAsync(added.Id, Groceries(shopping, 450m), CancellationToken);
        await service.DeleteBudgetAsync(added.Id, CancellationToken);
        List<Budget> afterDelete = await service.GetBudgetsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(read.Name).IsEqualTo("Groceries");
            await Assert.That(read.Limits.Single().Amount).IsEqualTo(400m);
            await Assert.That(month.Budgets.Single().Available).IsEqualTo(400m);
            await Assert.That(updated.MonthlyLimit).IsEqualTo(450m);
            await Assert.That(afterDelete).IsEmpty();
        }
    }

    [Test]
    public async Task AddBudget_Invalid_IsABadRequest()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IBudgetsService service = ApiFor(user).Get<IBudgetsService>();

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.AddBudgetAsync(new SaveBudget { Name = "Empty" }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetBudget_OfAnotherUser_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        ApiSession ownerApi = ApiFor(owner);
        Classification shopping = await ownerApi.Get<IClassificationService>()
            .AddCustomClassificationAsync(new AddClassifications { Tag = "Shopping" }, CancellationToken);
        Budget budget = await ownerApi.Get<IBudgetsService>().AddBudgetAsync(Groceries(shopping, 400m),
            CancellationToken);

        IBudgetsService service = ApiFor(other).Get<IBudgetsService>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetBudgetAsync(budget.Id, CancellationToken));
    }

    [Test]
    public async Task SplitTransaction_CountsOnlyItsPartsWithTheBudgetsTags()
    {
        // Arrange, a £100 shop of which £60 is groceries
        SignedInUser user = await RegisterUserAsync();
        ApiSession api = ApiFor(user);
        IClassificationService classifications = api.Get<IClassificationService>();
        Classification groceries = await classifications
            .AddCustomClassificationAsync(new AddClassifications { Tag = "Groceries" }, CancellationToken);
        Classification household = await classifications
            .AddCustomClassificationAsync(new AddClassifications { Tag = "Household" }, CancellationToken);
        IBudgetsService service = api.Get<IBudgetsService>();
        await service.AddBudgetAsync(Groceries(groceries, 400m), CancellationToken);
        DateTime now = DateTime.Now;
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("shop", "main", new TransactionDetailsFaker().Generate() with
            {
                Amount = -100m, Category = "PURCHASE", OccurredAt = DateTime.UtcNow, IsPending = false
            }));
        await api.Get<ITransactionsService>().SplitAsync(Scenario.TransactionMappings["shop"], new SplitTransaction
        {
            Parts =
            [
                new SplitTransactionPart { Amount = -60m, TagId = groceries.ClassificationId },
                new SplitTransactionPart { Amount = -30m, TagId = household.ClassificationId },
                new SplitTransactionPart { Amount = -10m }
            ]
        }, CancellationToken);

        // Act
        BudgetMonthOverview month = await service.GetMonthAsync(now.Year, now.Month, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(month.Budgets.Single().Spent).IsEqualTo(60m);
            await Assert.That(month.Unbudgeted).IsEqualTo(40m);
        }
    }

    private static SaveBudget Groceries(Classification tag, decimal limit) => new()
    {
        Name = "Groceries",
        TagIds = [tag.ClassificationId],
        MonthlyLimit = limit,
        RolloverMode = RolloverMode.CarryUnderspend
    };
}
