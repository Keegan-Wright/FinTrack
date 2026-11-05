using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Infrastructure.Exports.Statements;

/// <summary>
/// Gathers a <see cref="Statement"/> through the services the pages read, so it shows what they show, and only the
/// user's own.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<StatementBuilder>]
public sealed class StatementBuilder
{
    private readonly IBudgetsService _budgetsService;
    private readonly IBillsService _billsService;
    private readonly IDashboardService _dashboardService;
    private readonly IReportService _reportService;
    private readonly ITransactionsService _transactionsService;
    private readonly ISavingsGoalsService _savingsGoalsService;
    private readonly IDebtsService _debtsService;
    private readonly TimeProvider _timeProvider;

    public StatementBuilder(IBudgetsService budgetsService, IBillsService billsService,
        IDashboardService dashboardService, IReportService reportService, ITransactionsService transactionsService,
        ISavingsGoalsService savingsGoalsService, IDebtsService debtsService, TimeProvider timeProvider)
    {
        _budgetsService = budgetsService;
        _billsService = billsService;
        _dashboardService = dashboardService;
        _reportService = reportService;
        _transactionsService = transactionsService;
        _savingsGoalsService = savingsGoalsService;
        _debtsService = debtsService;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// The statement for the pay period named after <paramref name="month"/> of <paramref name="year"/>.
    /// </summary>
    public async Task<Statement> BuildAsync(int year, int month, CancellationToken cancellationToken)
    {
        // The budgets page's period for the month, from pay to pay once the user's pay is picked up
        BudgetMonthOverview budgets = await _budgetsService.GetMonthAsync(year, month, cancellationToken);
        BillMonthOverview bills = await _billsService.GetMonthAsync(year, month, cancellationToken);

        // The period's days where the user is, as the instants transactions are stored in
        TimeZoneInfo timeZone = _timeProvider.LocalTimeZone;
        DateTime fromUtc = StartOfDayUtc(budgets.Start, timeZone);
        DateTime untilUtc = StartOfDayUtc(budgets.End.AddDays(1), timeZone);
        FilterTransactions inPeriod = new() { FromDate = fromUtc, ToDate = untilUtc.AddTicks(-1) };

        SpentInTimePeriod totals = await _dashboardService.GetSpentInTimePeriod(fromUtc, untilUtc, cancellationToken);

        List<SpentInCategoryReport> tags = await _reportService
            .GetCategoryBreakdownReportAsync(inPeriod, cancellationToken)
            .Where(static x => x.TotalOut < 0)
            .OrderBy(static x => x.TotalOut)
            .ToListAsync(cancellationToken);

        List<Transaction> transactions = await _transactionsService
            .GetAllTransactionsAsync(inPeriod, cancellationToken)
            .OrderBy(static x => x.TransactionTime)
            .ToListAsync(cancellationToken);

        List<SavingsGoal> savingsGoals = await _savingsGoalsService.GetSavingsGoalsAsync(cancellationToken)
            .ToListAsync(cancellationToken);
        List<Debt> debts = await _debtsService.GetDebtsAsync(cancellationToken).ToListAsync(cancellationToken);

        return new Statement
        {
            Month = budgets.Month,
            Start = budgets.Start,
            End = budgets.End,
            IsCurrent = budgets.IsCurrent,
            MadeOn = _timeProvider.Today,
            Totals = totals,
            Tags = tags,
            Bills = bills,
            Budgets = budgets,
            SavingsGoals = savingsGoals,
            Debts = debts,
            Transactions = transactions
        };
    }

    private static DateTime StartOfDayUtc(DateOnly day, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), timeZone);
}
