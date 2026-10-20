using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared;

public class SqliteInMemoryDbContextFactory : IDbContextFactory<FinanceTrackerContext>
{
    private readonly DbContextOptions<FinanceTrackerContext> _options;
    private readonly ISymmetricEncryptionService _symmetricEncryptionService;
    public SqliteInMemoryDbContextFactory(DbContextOptions<FinanceTrackerContext> options,
        ISymmetricEncryptionService symmetricEncryptionService)
    {
        _options = options;
        _symmetricEncryptionService = symmetricEncryptionService;
    }

    public FinanceTrackerContext CreateDbContext() => new FinanceTrackerContext(_options, _symmetricEncryptionService);
}
