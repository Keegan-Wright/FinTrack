using FinanceTracker.Infrastructure.Common.Encryption;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence.EncryptedContext;

/// <summary>
/// Makes <see cref="TestEncryptedContext"/>s over one in-memory SQLite database, under a given keyring.
/// </summary>
public sealed class TestEncryptedContextFactory(SqliteConnection connection, IColumnEncryptionService encryptionService)
    : IDbContextFactory<TestEncryptedContext>
{
    public TestEncryptedContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TestEncryptedContext>().UseSqlite(connection).Options, encryptionService);
}
