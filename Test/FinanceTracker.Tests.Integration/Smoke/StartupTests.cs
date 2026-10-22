using System.Net;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Smoke;

public class StartupTests : IntegrationTestBase
{
    [Test]
    public async Task Health_ReportsHealthy()
    {
        // Arrange
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health", CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(await response.Content.ReadAsStringAsync(CancellationToken)).IsEqualTo("Healthy");
        }
    }

    [Test]
    public async Task Database_HasAllMigrationsApplied()
    {
        // Arrange
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        // Act
        IEnumerable<string> pending = await context.Database.GetPendingMigrationsAsync(CancellationToken);
        IEnumerable<string> applied = await context.Database.GetAppliedMigrationsAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(pending).IsEmpty();
            await Assert.That(applied).IsNotEmpty();
        }
    }

    [Test]
    public async Task SignedInUser_CanLoadTheHomePage()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();

        // Act
        HttpResponseMessage response = await user.Client.GetAsync("/", CancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
