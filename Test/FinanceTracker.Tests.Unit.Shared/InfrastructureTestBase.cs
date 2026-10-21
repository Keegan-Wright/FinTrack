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

    private DbContextOptions<FinanceTrackerContext> _options = null!;

    protected TestingScenario Scenario { get; private set; } = null!;
    protected IDbContextFactory<FinanceTrackerContext> DbContextFactory { get; private set; } = null!;

    protected IColumnEncryptionService ColumnEncryptionService { get; } =
        new ColumnEncryptionService(TestEncryptionKeys.Keyring());


    [Before(Test)]
    public async Task BaseSetupAsync()
    {
        await EnsureContextCreated();
        Scenario = new TestingScenario(DbContextFactory, CancellationToken);
    }

    private async Task EnsureContextCreated()
    {
        string uniqueDbName = $"UnitTestDb_{TestContext.Current!.Isolation.UniqueId}";
        string connectionString = $"Data Source=InMemory_{uniqueDbName};Mode=Memory;Cache=Shared";

        // The in memory database lives as long as one connection to it is open
        _masterConnection = new SqliteConnection(connectionString);
        await _masterConnection.OpenAsync(CancellationToken);

        _options = new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseSqlite(connectionString)
            .ReplaceService<IModelCustomizer, SqliteModelCustomizer>()
            .Options;

        DbContextFactory = CreateDbContextFactory(ColumnEncryptionService);

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await context.Database.EnsureCreatedAsync(CancellationToken);
    }

    /// <summary>
    /// Contexts on this test's database using <paramref name="encryptionService"/>, e.g. a rotated keyring.
    /// </summary>
    protected IDbContextFactory<FinanceTrackerContext> CreateDbContextFactory(
        IColumnEncryptionService encryptionService) =>
        new SqliteInMemoryDbContextFactory(_options, encryptionService);

    /// <summary>
    /// The current user services act for, as a user seeded through the scenario.
    /// </summary>
    protected ICurrentUser CurrentUserFor(string username) => CurrentUserFor(Scenario.UserMappings[username]);

    protected static ICurrentUser CurrentUserFor(Guid userId)
    {
        CurrentUser currentUser = new();
        currentUser.Set(userId);
        return currentUser;
    }

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
