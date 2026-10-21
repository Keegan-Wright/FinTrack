using System.Security.Claims;
using FinanceTracker.Infrastructure.Budget;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public class ServiceBaseTests : InfrastructureTestBase
{
    [Test]
    public async Task UserId_PrincipalWithoutNameIdentifier_Throws()
    {
        // Arrange
        ClaimsPrincipal anonymous = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "someone")], "test"));
        BudgetCategoriesService subject = new(anonymous, DbContextFactory, NullLogger<BudgetCategoriesService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await subject.GetBudgetItemsAsync(CancellationToken).ToListAsync(CancellationToken));
    }
}
