using FinanceTracker.Domain.Identity;
using FinanceTracker.Tests.Unit.Shared.Fakers;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class UserScenario : ScenarioBuilder
{
    public UserScenario(TestingScenario scenario) : base(scenario)
    {
    }

    public Task<TestingScenario> WithUser(string firstName, string lastName, string email, string username) =>
        WithUser(new FinanceTrackerUser(firstName, lastName, email, username));

    /// <summary>
    /// Adds a user with generated details, mapped by <paramref name="username"/>.
    /// </summary>
    public Task<TestingScenario> WithUser(string username) =>
        WithUser(new FinanceTrackerUserFaker(username).Generate());

    private async Task<TestingScenario> WithUser(FinanceTrackerUser user)
    {
        await using var context = await Scenario.DbContextFactory.CreateDbContextAsync(Scenario.CancellationToken);

        var savedUser = await context.Users.AddAsync(user, Scenario.CancellationToken);

        await context.SaveChangesAsync(Scenario.CancellationToken);

        Scenario.UserMappings.Add(savedUser.Entity.UserName!, savedUser.Entity.Id);

        return Scenario;
    }
}
