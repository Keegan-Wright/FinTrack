using FinanceTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public sealed class TestingScenario
{
    internal IDbContextFactory<FinanceTrackerContext> DbContextFactory { get; init; }
    internal CancellationToken CancellationToken { get; init; }

    public DebtScenario DebtScenario { get; init; }
    public UserScenario UserScenario { get; init; }

    public Dictionary<string, Guid> UserMappings { get; set; } = [];

    public TestingScenario(IDbContextFactory<FinanceTrackerContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        DbContextFactory = dbContextFactory;
        CancellationToken = cancellationToken;

        DebtScenario = new DebtScenario(this);
        UserScenario = new UserScenario(this);
    }

}
