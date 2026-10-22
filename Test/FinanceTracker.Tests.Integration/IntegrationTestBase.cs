using System.Net;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Base for tests against the running app. The app and its database are shared by every test, so tests stay
/// isolated by working with their own uniquely named users rather than resetting data.
/// </summary>
public abstract class IntegrationTestBase
{
    private readonly List<ApiSession> _apiSessions = [];

    private CancellationTokenSource CancellationTokenSource { get; set; } = null!;

    [ClassDataSource<FinTrackAppFixture>(Shared = SharedType.PerTestSession)]
    public required FinTrackAppFixture App { get; init; }

    protected CancellationToken CancellationToken => CancellationTokenSource.Token;

    protected IDbContextFactory<FinanceTrackerContext> DbContextFactory => App.DbContextFactory;

    protected TestingScenario Scenario { get; private set; } = null!;

    [Before(Test)]
    public void BaseIntegrationSetup()
    {
        CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        Scenario = new TestingScenario(DbContextFactory, CancellationToken);
    }

    [After(Test)]
    public async Task BaseIntegrationCleanupAsync()
    {
        foreach (ApiSession session in _apiSessions)
        {
            await session.DisposeAsync();
        }

        await CancellationTokenSource.CancelAsync();
        CancellationTokenSource.Dispose();
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
    /// Calls the API as <paramref name="user"/> through the front end's API clients, disposed after the test.
    /// </summary>
    protected ApiSession ApiFor(SignedInUser user)
    {
        ApiSession session = new(user.Client);
        _apiSessions.Add(session);
        return session;
    }

    /// <summary>
    /// Registers a new user and makes them an admin. Only the first user registers as an admin
    /// (<see cref="FinTrackAppFixture.FirstUser"/>), so the role is granted in the database, then they sign in again
    /// as role claims are read at sign in.
    /// </summary>
    protected async Task<SignedInUser> RegisterAdminAsync()
    {
        SignedInUser user = await RegisterUserAsync();

        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            Guid adminRoleId = await context.Roles.Where(x => x.Name == FinTrackRoles.Admin).Select(x => x.Id)
                .SingleAsync(CancellationToken);
            context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.UserId, RoleId = adminRoleId });
            await context.SaveChangesAsync(CancellationToken);
        }

        FinTrackClient client = CreateClient();
        HttpResponseMessage response = await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);
        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Signing {user.Username} back in returned {response.StatusCode}");
        }

        return user with { Client = client };
    }

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
