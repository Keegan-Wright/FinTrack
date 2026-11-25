using FinanceTracker.Ai.Persistence;
using FinanceTracker.Ai.Services;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Tests.Unit.Ai;

/// <summary>
/// An <see cref="AiService"/> over an in-memory database, with the environment a test gives it.
/// </summary>
public abstract class AiServiceTestBase : UnitTestBase, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected TestCurrentUser CurrentUser { get; } = new();

    protected TestAiContextFactory ContextFactory { get; }

    protected AiServiceTestBase() => ContextFactory = new TestAiContextFactory(_connection);

    [Before(Test)]
    public async Task CreateDatabaseAsync()
    {
        await _connection.OpenAsync();
        await using FinanceTrackerAiContext context = ContextFactory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The service, with <paramref name="environmentEndpoint"/> as OLLAMA_ENDPOINT, unset when null.
    /// </summary>
    protected AiService Service(string? environmentEndpoint = null)
    {
        Dictionary<string, string?> environment = [];
        if (environmentEndpoint is not null)
        {
            environment[AiService.OllamaEndpointVariable] = environmentEndpoint;
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(environment).Build();
        return new AiService(CurrentUser, ContextFactory, configuration);
    }
}
