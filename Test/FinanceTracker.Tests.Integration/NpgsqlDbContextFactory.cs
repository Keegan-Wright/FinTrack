using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Creates contexts against the AppHost's Postgres database, for seeding and asserting from tests.
/// </summary>
internal sealed class NpgsqlDbContextFactory(
    DbContextOptions<FinanceTrackerContext> options,
    IColumnEncryptionService columnEncryptionService) : IDbContextFactory<FinanceTrackerContext>
{
    public FinanceTrackerContext CreateDbContext() => new(options, columnEncryptionService, TimeProvider.System);
}
