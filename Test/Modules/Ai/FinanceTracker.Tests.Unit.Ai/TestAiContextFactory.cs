using FinanceTracker.Ai.Persistence;
using FinanceTracker.Infrastructure.Common.Encryption;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Ai;

/// <summary>
/// Makes the AI module's contexts over one in-memory SQLite database, encrypted with a test key.
/// </summary>
public sealed class TestAiContextFactory(SqliteConnection connection) : IDbContextFactory<FinanceTrackerAiContext>
{
    private readonly ColumnEncryptionService _encryption = new(TestEncryptionKeys.Keyring(currentKeyId: 1, 1));

    public FinanceTrackerAiContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FinanceTrackerAiContext>().UseSqlite(connection).Options, _encryption);
}
