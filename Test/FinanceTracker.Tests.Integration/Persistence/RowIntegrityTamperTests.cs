using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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

    [Test]
    public async Task Rotate_EverythingAlreadyTagged_FindsNothingStale()
    {
        // Arrange, a balance in the account's row and alerts as jsonb, which Postgres writes back with spaces
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BankAccountScenario.WithBalance("main"));
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            NotificationSettings settings = new(Scenario.UserMappings[user.Username]);
            settings.WatchBalances(new Dictionary<Guid, decimal> { [Scenario.AccountMappings["main"]] = 100m });
            context.NotificationSettings.Add(settings);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        int reencrypted = await new EncryptionKeyRotator(DbContextFactory, NullLogger<EncryptionKeyRotator>.Instance)
            .RotateAsync(CancellationToken);

        // Assert, so startup doesn't rewrite them every time
        await Assert.That(reencrypted).IsEqualTo(0);
    }
}
