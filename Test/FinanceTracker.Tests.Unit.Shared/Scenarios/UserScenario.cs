using FinanceTracker.Domain.Identity;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class UserScenario : ScenarioBuilder
{
    public UserScenario(TestingScenario scenario) : base(scenario)
    {
    }

    public async Task<TestingScenario> WithUser(string firstName, string lastName, string email, string username)
    {
        var user = new FinanceTrackerUser(firstName, lastName, email, username);

        await using var context = await Scenario.DbContextFactory.CreateDbContextAsync(Scenario.CancellationToken);

        var savedUser = await context.Users.AddAsync(user);

        await context.SaveChangesAsync(Scenario.CancellationToken);

        Scenario.UserMappings.Add(savedUser.Entity.UserName!, savedUser.Entity.Id);

        return Scenario;
    }

}
