using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared;

public class SqliteInMemoryDbContextFactory : IDbContextFactory<FinanceTrackerContext>
{
    private readonly DbContextOptions<FinanceTrackerContext> _options;
    private readonly IColumnEncryptionService _columnEncryptionService;
    public SqliteInMemoryDbContextFactory(DbContextOptions<FinanceTrackerContext> options,
        IColumnEncryptionService columnEncryptionService)
    {
        _options = options;
        _columnEncryptionService = columnEncryptionService;
    }

    public FinanceTrackerContext CreateDbContext() => new FinanceTrackerContext(_options, _columnEncryptionService, TimeProvider.System);
}
