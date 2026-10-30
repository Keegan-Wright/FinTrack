using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.SavingsGoals;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public class ServiceBaseTests : InfrastructureTestBase
{
    [Test]
    public async Task UserId_WhenNoUserIsSet_Throws()
    {
        // Arrange
        SavingsGoalsService subject = new(new CurrentUser(), DbContextFactory,
            NullLogger<SavingsGoalsService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await subject.GetSavingsGoalsAsync(CancellationToken).ToListAsync(CancellationToken));
    }
}
