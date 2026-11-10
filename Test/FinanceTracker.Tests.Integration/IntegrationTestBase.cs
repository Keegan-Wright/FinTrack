using System.Globalization;
using System.Net;
using FinanceTracker.AppHost.ServiceDefaults;
using FinanceTracker.Contracts.Auth;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using FinanceTracker.Tests.Unit.Shared;

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
        CancellationTokenSource = new CancellationTokenSource(TestTimeouts.IntegrationTest);
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
    /// When a user registered without a day of their own is, null for now, see <see cref="RegisterUserAsync"/>.
    /// </summary>
    protected virtual DateTimeOffset? DefaultClock => null;

    /// <summary>
    /// Moves the app's clock <paramref name="offset"/> from the real one for what <paramref name="client"/> does, none
    /// for no offset, see <see cref="TestClock"/>.
    /// </summary>
    private void SetClock(FinTrackClient client, TimeSpan offset)
    {
        if (offset == TimeSpan.Zero)
        {
            return;
        }

        client.Cookies.Add(new Cookie(TestClock.Cookie, offset.TotalSeconds.ToString(CultureInfo.InvariantCulture),
            "/", App.WebBaseAddress.Host));
    }

    /// <summary>
    /// Now for <paramref name="user"/>, in UTC, as the app reads it for them, for dating what's seeded for them.
    /// </summary>
    protected static DateTime NowFor(SignedInUser user) => DateTime.UtcNow + user.ClockOffset;

    /// <summary>
    /// Today for <paramref name="user"/> where the app runs, as it reads it.
    /// </summary>
    protected static DateOnly TodayFor(SignedInUser user) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(NowFor(user), TimeZoneInfo.Local));

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

        await Scenario.Do(s => s.UserScenario.WithRole(user.Username, FinTrackRoles.Admin));

        FinTrackClient client = CreateClient();
        SetClock(client, user.ClockOffset);
        HttpResponseMessage response = await client.LoginAsync(user.Username, SignedInUser.DefaultPassword, CancellationToken);
        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Signing {user.Username} back in returned {response.StatusCode}");
        }

        return user with { Client = client };
    }

    /// <summary>
    /// Registers a new user through the register page and returns a client signed in as them. The user is
    /// added to <see cref="Scenario"/> under its username so scenarios can build data for it. Given
    /// <paramref name="at"/>, the app's clock reads that for everything the user does from registering on, time
    /// passing from there, see <see cref="TestClock"/>. Their email is confirmed, as an account's is once its link is
    /// opened, unless <paramref name="confirmEmail"/> is false.
    /// </summary>
    protected async Task<SignedInUser> RegisterUserAsync(string? username = null, DateTimeOffset? at = null,
        bool confirmEmail = true)
    {
        username ??= UniqueName();
        at ??= DefaultClock;
        FinTrackClient client = CreateClient();
        TimeSpan offset = TimeSpan.Zero;
        if (at is { } moment)
        {
            offset = TimeSpan.FromSeconds(Math.Round((moment - DateTimeOffset.UtcNow).TotalSeconds));
            SetClock(client, offset);
        }

        HttpResponseMessage response = await client.RegisterAsync(username, SignedInUser.DefaultPassword, CancellationToken);
        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException(
                $"Registering {username} returned {response.StatusCode}: {await response.Content.ReadAsStringAsync(CancellationToken)}");
        }

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid userId = await context.Users.Where(x => x.UserName == username).Select(x => x.Id).SingleAsync(CancellationToken);

        Scenario.UserMappings[username] = userId;
        if (confirmEmail)
        {
            await context.Users.Where(x => x.Id == userId)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.EmailConfirmed, true), CancellationToken);
        }

        return new SignedInUser(username, userId, client) { ClockOffset = offset };
    }
}
