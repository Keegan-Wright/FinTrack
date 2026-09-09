using FinanceTracker.Domain;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Tests.Services.Infrastructure;

public class FinanceTrackerContextInMemoryEncryptionTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task InMemoryContext_WithSymmetricEncryptionService_CanPersistAndReadEncryptedEntities()
    {
        // Arrange
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"ctx-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        // Act
        await using (FinanceTrackerContext writeContext = await factory.CreateDbContextAsync(_cancellationTokenSource.Token))
        {
            FinanceTrackerUser user = await writeContext.Users.Include(x => x.BudgetCategories)
                .SingleAsync(x => x.Id == PrimaryUserId, _cancellationTokenSource.Token);

            user.BudgetCategories!.Add(new BudgetCategory
            {
                Name = "Encrypted Budget",
                AvailableFunds = 77,
                MonthlyStart = 3,
                SavingsGoal = 500,
                GoalCompletionDate = DateTime.UtcNow.Date.AddMonths(1),
                Created = DateTime.UtcNow
            });

            await writeContext.SaveChangesAsync(_cancellationTokenSource.Token);
        }

        await using FinanceTrackerContext readContext = await factory.CreateDbContextAsync(_cancellationTokenSource.Token);
        BudgetCategory loaded = await readContext.BudgetCategories.SingleAsync(_cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(loaded.Name).IsEqualTo("Encrypted Budget");
            await Assert.That(loaded.AvailableFunds).IsEqualTo(77);
        }
    }
}
