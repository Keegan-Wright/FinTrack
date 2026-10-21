using System.Net;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Base for tests against the running app. The app and its database are shared by every test, so tests stay
/// isolated by working with their own uniquely named users rather than resetting data.
/// </summary>
public abstract class IntegrationTestBase
{
    private CancellationTokenSource _cancellationTokenSource = null!;

    [ClassDataSource<FinTrackAppFixture>(Shared = SharedType.PerTestSession)]
    public required FinTrackAppFixture App { get; init; }

    protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

    protected IDbContextFactory<FinanceTrackerContext> DbContextFactory => App.DbContextFactory;

    protected TestingScenario Scenario { get; private set; } = null!;

    [Before(Test)]
    public void BaseIntegrationSetup()
    {
        _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        Scenario = new TestingScenario(DbContextFactory, CancellationToken);
    }

    [After(Test)]
    public async Task BaseIntegrationCleanupAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();
    }

    /// <summary>
    /// A unique name so data from one test never collides with another in the shared database.
    /// </summary>
    protected static string UniqueName(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>
    /// A client with its own cookies that does not follow redirects.
    /// </summary>
    protected FinTrackClient CreateClient() => new(App.WebBaseAddress);

    /// <summary>
    /// Registers a new user through the register page and returns a client signed in as them. The user is
    /// added to <see cref="Scenario"/> under its username so scenarios can build data for it.
    /// </summary>
    protected async Task<SignedInUser> RegisterUserAsync(string? username = null)
    {
        username ??= UniqueName();
        FinTrackClient client = CreateClient();

        HttpResponseMessage response = await client.RegisterAsync(username, SignedInUser.DefaultPassword, CancellationToken);
        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"Registering {username} returned {response.StatusCode}: {await response.Content.ReadAsStringAsync(CancellationToken)}");
        }

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid userId = await context.Users.Where(x => x.UserName == username).Select(x => x.Id).SingleAsync(CancellationToken);

        Scenario.UserMappings[username] = userId;

        return new SignedInUser(username, userId, client);
    }
}
