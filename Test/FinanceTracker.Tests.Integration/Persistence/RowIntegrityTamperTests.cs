using FinanceTracker.Contracts.Services;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Persistence;

/// <summary>
/// Someone with write access to the database but not the key, moving a user's data to another user with SQL. The
/// running app refuses the row rather than showing it.
/// </summary>
public class RowIntegrityTamperTests : IntegrationTestBase
{
    [Test]
    public async Task DebtMovedToAnotherUser_IsntShownToThem()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        await Scenario.Do(s => s.DebtScenario.WithDebt("loan", owner.Username));
        Guid debtId = Scenario.DebtMappings["loan"];
        Guid otherId = Scenario.UserMappings[other.Username];
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            await context.Database.ExecuteSqlAsync(
                $"""UPDATE "Debts" SET "UserId" = {otherId} WHERE "Id" = {debtId}""", CancellationToken);
        }

        // Act
        IDebtsService service = ApiFor(other).Get<IDebtsService>();

        // Assert, the request fails rather than handing them the debt, as the row fails its check
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetDebtsAsync(CancellationToken).ToListAsync(CancellationToken).AsTask());
        await using FinanceTrackerContext check = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.ThrowsAsync<RowIntegrityException>(() =>
            check.Debts.Where(x => x.Id == debtId).ToListAsync(CancellationToken));
    }
}
