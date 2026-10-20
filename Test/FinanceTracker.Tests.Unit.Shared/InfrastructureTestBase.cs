using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared;

public abstract class InfrastructureTestBase : IAsyncDisposable
{
    private SqliteConnection? _masterConnection;


    protected TestingScenario Scenario { get; private set; } = null!;
    protected IDbContextFactory<FinanceTrackerContext> DbContextFactory { get; private set; } = null!;
    protected CancellationToken CancellationToken => CancellationTokenSource.Token;
    private CancellationTokenSource CancellationTokenSource { get; set; } = null!;

    private readonly ISymmetricEncryptionService _symmetricEncryptionService = new SymmetricEncryptionService(
        new EncryptionConfiguration()
        {
            Iterations = 10,
            SymmetricKey = "SymmetricKeyForUnitTesting",
            SymmetricSalt = "SymmetricSaltForUnitTesting"
        });


    [Before(Test)]
    public async Task BaseSetupAsync()
    {
        // 1. Initialize a clean CancellationTokenSource for this specific test execution
        CancellationTokenSource = new CancellationTokenSource();

        // 2. Generate a completely isolated database name using TUnit's unique execution ID
        var uniqueDbName = $"UnitTestDb_{TestContext.Current.Isolation.UniqueId}";
        var connectionString = $"Data Source=InMemory_{uniqueDbName};Mode=Memory;Cache=Shared";

        _masterConnection = new SqliteConnection(connectionString);
        await _masterConnection.OpenAsync(CancellationToken);

        var options = new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseSqlite(connectionString)
            .Options;

        DbContextFactory = new SqliteInMemoryDbContextFactory(options, _symmetricEncryptionService);

        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await context.Database.EnsureCreatedAsync(CancellationToken);

        Scenario = new TestingScenario(DbContextFactory, CancellationToken);

    }

    [After(Test)]
    public async Task BaseCleanupAsync()
    {
        // Cancel and safely dispose of the token source after the test finishes
        if (CancellationTokenSource != null)
        {
            await CancellationTokenSource.CancelAsync();
            CancellationTokenSource.Dispose();
        }

        if (_masterConnection != null)
        {
            await _masterConnection.CloseAsync();
            await _masterConnection.DisposeAsync();
        }
    }


    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}
