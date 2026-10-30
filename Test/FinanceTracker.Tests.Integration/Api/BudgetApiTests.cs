using System.Net;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
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

    private static SaveBudget Groceries(Classification tag, decimal limit) => new()
    {
        Name = "Groceries",
        TagIds = [tag.ClassificationId],
        MonthlyLimit = limit,
        RolloverMode = RolloverMode.CarryUnderspend
    };
}
