using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

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
            writeContext.BudgetCategories.Add(new BudgetCategory(PrimaryUserId, "Encrypted Budget", 77, 3, 500, DateTime.UtcNow.Date.AddMonths(1)));

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
