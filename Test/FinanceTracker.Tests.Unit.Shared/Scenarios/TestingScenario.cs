using FinanceTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Seeds a test database. Each builder maps the data it adds by a test chosen name so later steps, and the
/// test itself, can refer back to it.
/// </summary>
public sealed class TestingScenario
{
    /// <summary>
    /// Makes the contexts the builders save through.
    /// </summary>
    internal IDbContextFactory<FinanceTrackerContext> DbContextFactory { get; init; }

    /// <summary>
    /// The test's cancellation token, passed to every query and save.
    /// </summary>
    internal CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Seeds users.
    /// </summary>
    public UserScenario UserScenario { get; init; }

    /// <summary>
    /// Seeds bank connections.
    /// </summary>
    public BankConnectionScenario BankConnectionScenario { get; init; }

    /// <summary>
    /// Seeds bank accounts.
    /// </summary>
    public BankAccountScenario BankAccountScenario { get; init; }

    /// <summary>
    /// Seeds transactions.
    /// </summary>
    public TransactionScenario TransactionScenario { get; init; }

    /// <summary>
    /// Seeds tags.
    /// </summary>
    public ClassificationScenario ClassificationScenario { get; init; }

    /// <summary>
    /// Seeds savings goals.
    /// </summary>
    public SavingsGoalScenario SavingsGoalScenario { get; init; }

    /// <summary>
    /// Seeds debts.
    /// </summary>
    public DebtScenario DebtScenario { get; init; }

    /// <summary>
    /// Seeds household members.
    /// </summary>
    public HouseholdMemberScenario HouseholdMemberScenario { get; init; }

    /// <summary>
    /// Seeds data protection keys.
    /// </summary>
    public DataProtectionScenario DataProtectionScenario { get; init; }

    /// <summary>
    /// Seeds rules.
    /// </summary>
    public RuleScenario RuleScenario { get; init; }

    /// <summary>
    /// Seeds budgets.
    /// </summary>
    public BudgetScenario BudgetScenario { get; init; }

    /// <summary>
    /// Seeds bills and bill suggestions.
    /// </summary>
    public BillScenario BillScenario { get; init; }

    /// <summary>
    /// Seeds pay.
    /// </summary>
    public PayScenario PayScenario { get; init; }

    /// <summary>
    /// Seeds calendar events.
    /// </summary>
    public CalendarEventScenario CalendarEventScenario { get; init; }

    /// <summary>
    /// Seeds notifications.
    /// </summary>
    public NotificationScenario NotificationScenario { get; init; }

    /// <summary>
    /// The ids of the users added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> UserMappings { get; set; } = [];

    /// <summary>
    /// The ids of the bank connections added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> ConnectionMappings { get; set; } = [];

    /// <summary>
    /// The ids of the bank accounts added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> AccountMappings { get; set; } = [];

    /// <summary>
    /// The ids of the transactions added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> TransactionMappings { get; set; } = [];

    /// <summary>
    /// The ids of the tags added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> ClassificationMappings { get; set; } = [];

    /// <summary>
    /// The ids of the savings goals added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> SavingsGoalMappings { get; set; } = [];

    /// <summary>
    /// The ids of the debts added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> DebtMappings { get; set; } = [];

    /// <summary>
    /// The ids of the household members added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> HouseholdMemberMappings { get; set; } = [];

    /// <summary>
    /// The ids of the rules added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> RuleMappings { get; set; } = [];

    /// <summary>
    /// The ids of the budgets added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> BudgetMappings { get; set; } = [];

    /// <summary>
    /// The ids of the bills added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> BillMappings { get; set; } = [];

    /// <summary>
    /// The ids of the bill suggestions added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> BillSuggestionMappings { get; set; } = [];

    /// <summary>
    /// The ids of the calendar events added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> CalendarEventMappings { get; set; } = [];

    /// <summary>
    /// The ids of the notifications added, by the name the test gave each.
    /// </summary>
    public Dictionary<string, Guid> NotificationMappings { get; set; } = [];

    /// <param name="dbContextFactory">Makes the contexts the builders save through.</param>
    /// <param name="cancellationToken">The test's cancellation token.</param>
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
        DataProtectionScenario = new DataProtectionScenario(this);
        RuleScenario = new RuleScenario(this);
        BudgetScenario = new BudgetScenario(this);
        BillScenario = new BillScenario(this);
        PayScenario = new PayScenario(this);
        CalendarEventScenario = new CalendarEventScenario(this);
        NotificationScenario = new NotificationScenario(this);
    }
}
