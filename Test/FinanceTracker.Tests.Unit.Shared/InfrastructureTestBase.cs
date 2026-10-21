using System.Security.Claims;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FinanceTracker.Tests.Unit.Shared;

/// <summary>
/// Base for tests that need a database. Each test gets its own SQLite in memory database and a fresh
/// <see cref="TestingScenario"/> to seed it.
/// </summary>
public abstract class InfrastructureTestBase : UnitTestBase, IAsyncDisposable
{
    private SqliteConnection? _masterConnection;

    protected TestingScenario Scenario { get; private set; } = null!;
    protected IDbContextFactory<FinanceTrackerContext> DbContextFactory { get; private set; } = null!;

    protected ISymmetricEncryptionService SymmetricEncryptionService { get; } = new SymmetricEncryptionService(
        new EncryptionConfiguration()
        {
            Iterations = 10,
            SymmetricKey = "SymmetricKeyForUnitTesting",
            SymmetricSalt = "SymmetricSaltForUnitTesting"
        });


    [Before(Test)]
    public async Task BaseSetupAsync()
    {
        await EnsureContextCreated();
        Scenario = new TestingScenario(DbContextFactory, CancellationToken);
    }

    private async Task EnsureContextCreated()
    {
        var uniqueDbName = $"UnitTestDb_{TestContext.Current!.Isolation.UniqueId}";
        var connectionString = $"Data Source=InMemory_{uniqueDbName};Mode=Memory;Cache=Shared";

        // The in memory database lives as long as one connection to it is open
        _masterConnection = new SqliteConnection(connectionString);
        await _masterConnection.OpenAsync(CancellationToken);

        var options = new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseSqlite(connectionString)
            .ReplaceService<IModelCustomizer, SqliteModelCustomizer>()
            .Options;

        DbContextFactory = new SqliteInMemoryDbContextFactory(options, SymmetricEncryptionService);

        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await context.Database.EnsureCreatedAsync(CancellationToken);
    }

    /// <summary>
    /// Builds the principal services resolve the current user from, for a user seeded through the scenario.
    /// </summary>
    protected ClaimsPrincipal UserPrincipal(string username) => BuildPrincipal(Scenario.UserMappings[username]);

    protected static ClaimsPrincipal BuildPrincipal(Guid userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));

    [After(Test)]
    public async Task BaseCleanupAsync()
    {
        await _masterConnection!.CloseAsync();
        await _masterConnection.DisposeAsync();
    }


    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}
