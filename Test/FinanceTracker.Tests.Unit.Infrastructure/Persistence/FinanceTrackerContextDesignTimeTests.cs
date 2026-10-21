using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

public class FinanceTrackerContextDesignTimeTests : UnitTestBase
{
    [Test]
    public async Task CreateDbContext_ConfiguresPostgresWithTheMigrationsAssembly()
    {
        // Arrange
#pragma warning disable CS0618 // Design time constructor, as used by the EF tooling
        FinanceTrackerContext factory = new();
#pragma warning restore CS0618

        // Act
        await using FinanceTrackerContext context = factory.CreateDbContext([]);

        // Assert
#pragma warning disable EF1001 // The provider's options extension is the only place the migrations assembly is exposed
        NpgsqlOptionsExtension npgsql = context.GetService<IDbContextOptions>().FindExtension<NpgsqlOptionsExtension>()!;
#pragma warning restore EF1001

        using (Assert.Multiple())
        {
            await Assert.That(context.Database.ProviderName).IsEqualTo("Npgsql.EntityFrameworkCore.PostgreSQL");
            await Assert.That(npgsql.MigrationsAssembly).IsEqualTo("FinanceTracker.Infrastructure.Migrations");
            await Assert.That(npgsql.CommandTimeout).IsEqualTo(0);
            await Assert.That(context.Model.FindEntityType(typeof(Domain.Budgeting.Debt))).IsNotNull();
        }
    }
}
