using FinanceTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds a test database. Each builder maps the data it adds by a test chosen name so later steps, and the
/// test itself, can refer back to it.
/// </summary>
public sealed class TestingScenario
{
    internal IDbContextFactory<FinanceTrackerContext> DbContextFactory { get; init; }
    internal CancellationToken CancellationToken { get; init; }

    public UserScenario UserScenario { get; init; }
    public BankConnectionScenario BankConnectionScenario { get; init; }
    public BankAccountScenario BankAccountScenario { get; init; }
    public TransactionScenario TransactionScenario { get; init; }
    public ClassificationScenario ClassificationScenario { get; init; }
    public SavingsGoalScenario SavingsGoalScenario { get; init; }
    public DebtScenario DebtScenario { get; init; }
    public HouseholdMemberScenario HouseholdMemberScenario { get; init; }
    public AutomationScenario AutomationScenario { get; init; }
    public DataProtectionScenario DataProtectionScenario { get; init; }
    public RuleScenario RuleScenario { get; init; }

    public Dictionary<string, Guid> UserMappings { get; set; } = [];
    public Dictionary<string, Guid> ConnectionMappings { get; set; } = [];
    public Dictionary<string, Guid> AccountMappings { get; set; } = [];
    public Dictionary<string, Guid> TransactionMappings { get; set; } = [];
    public Dictionary<string, Guid> ClassificationMappings { get; set; } = [];
    public Dictionary<string, Guid> SavingsGoalMappings { get; set; } = [];
    public Dictionary<string, Guid> DebtMappings { get; set; } = [];
    public Dictionary<string, Guid> HouseholdMemberMappings { get; set; } = [];
    public Dictionary<string, Guid> CronTickerMappings { get; set; } = [];
    public Dictionary<string, Guid> RuleMappings { get; set; } = [];

    public TestingScenario(IDbContextFactory<FinanceTrackerContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        DbContextFactory = dbContextFactory;
        CancellationToken = cancellationToken;

        UserScenario = new UserScenario(this);
        BankConnectionScenario = new BankConnectionScenario(this);
        BankAccountScenario = new BankAccountScenario(this);
        TransactionScenario = new TransactionScenario(this);
        ClassificationScenario = new ClassificationScenario(this);
        SavingsGoalScenario = new SavingsGoalScenario(this);
        DebtScenario = new DebtScenario(this);
        HouseholdMemberScenario = new HouseholdMemberScenario(this);
        AutomationScenario = new AutomationScenario(this);
        DataProtectionScenario = new DataProtectionScenario(this);
        RuleScenario = new RuleScenario(this);
    }

}
