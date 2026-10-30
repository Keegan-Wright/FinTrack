namespace FinanceTracker.Contracts.Api;

/// <summary>
/// Where each service's endpoints live, relative to the app's base address. The server maps its endpoint groups at
/// these and the front end's HTTP clients call under them. The integration tests call every endpoint through those
/// clients, which catches the paths within a group drifting apart.
/// </summary>
/// <remarks>
/// Every endpoint needs a signed in user and the <see cref="ApiHeaders.Request"/> header, and acts for that user
/// only. Lists stream as JSON arrays.
/// </remarks>
public static class ApiRoutes
{
    /// <summary>
    /// <see cref="Services.IAccountService"/>.
    /// </summary>
    public const string Accounts = "api/accounts";

    /// <summary>
    /// The background jobs of <see cref="Services.IAutomationService"/>, admins only.
    /// </summary>
    public const string AutomationJobs = "api/automation/jobs";

    /// <summary>
    /// The user's own syncs, from <see cref="Services.IAutomationService"/>.
    /// </summary>
    public const string Sync = "api/sync";

    /// <summary>
    /// <see cref="Services.IBudgetCategoriesService"/>.
    /// </summary>
    public const string BudgetCategories = "api/budget-categories";

    /// <summary>
    /// <see cref="Services.ICalendarService"/>.
    /// </summary>
    public const string Calendar = "api/calendar";

    /// <summary>
    /// <see cref="Services.IClassificationService"/>.
    /// </summary>
    public const string Classifications = "api/classifications";

    /// <summary>
    /// <see cref="Services.IDashboardService"/>.
    /// </summary>
    public const string Dashboard = "api/dashboard";

    /// <summary>
    /// <see cref="Services.IHouseholdMemberService"/>.
    /// </summary>
    public const string HouseholdMembers = "api/household-members";

    /// <summary>
    /// <see cref="Services.IOpenBankingService"/>.
    /// </summary>
    public const string OpenBanking = "api/open-banking";

    /// <summary>
    /// <see cref="Services.IReportService"/>. Each report is a POST taking the same
    /// <see cref="Transactions.FilterTransactions"/> body.
    /// </summary>
    public const string Reports = "api/reports";

    /// <summary>
    /// <see cref="Services.IRulesService"/>.
    /// </summary>
    public const string Rules = "api/rules";

    /// <summary>
    /// <see cref="Services.ITransactionsService"/>.
    /// </summary>
    public const string Transactions = "api/transactions";

    /// <summary>
    /// <see cref="Services.ITransfersService"/>.
    /// </summary>
    public const string Transfers = "api/transfers";
}
