using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Budget;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public class ServiceBaseTests : InfrastructureTestBase
{
    [Test]
    public async Task UserId_WhenNoUserIsSet_Throws()
    {
        // Arrange
        BudgetCategoriesService subject = new(new CurrentUser(), DbContextFactory,
            NullLogger<BudgetCategoriesService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await subject.GetBudgetItemsAsync(CancellationToken).ToListAsync(CancellationToken));
    }
}
