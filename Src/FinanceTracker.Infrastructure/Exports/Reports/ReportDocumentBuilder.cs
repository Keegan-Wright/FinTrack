using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;

namespace FinanceTracker.Infrastructure.Exports.Reports;

/// <summary>
/// Gathers a <see cref="ReportContent"/> through the report service the reports page reads, so it shows what the page
/// shows, and names the filters with what the filter bar lists.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<ReportDocumentBuilder>]
public sealed class ReportDocumentBuilder
{
    private readonly IReportService _reportService;
    private readonly ITransactionsService _transactionsService;
    private readonly IBillsService _billsService;
    private readonly IDebtsService _debtsService;
    private readonly ISavingsGoalsService _savingsGoalsService;
    private readonly TimeProvider _timeProvider;

    public ReportDocumentBuilder(IReportService reportService, ITransactionsService transactionsService,
        IBillsService billsService, IDebtsService debtsService, ISavingsGoalsService savingsGoalsService,
        TimeProvider timeProvider)
    {
        _reportService = reportService;
        _transactionsService = transactionsService;
        _billsService = billsService;
        _debtsService = debtsService;
        _savingsGoalsService = savingsGoalsService;
        _timeProvider = timeProvider;
    }

    public async Task<ReportContent> BuildAsync(ExportReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        FilterTransactions filters = report.Filters;
        List<ReportRow> subjects = await SubjectsAsync(report.Breakdown, filters, cancellationToken);

        return new ReportContent
        {
            Breakdown = report.Breakdown,
            Subjects = subjects
                .Where(static x => x.TotalTransactions > 0)
                .OrderBy(static x => x.TotalOut)
                .ToList(),
            From = Day(filters.FromDate),
            To = Day(filters.ToDate),
            Filters = await DescribeAsync(filters, cancellationToken),
            MadeOn = _timeProvider.Today
        };
    }

    private async Task<List<ReportRow>> SubjectsAsync(ReportBreakdown breakdown, FilterTransactions filters,
        CancellationToken cancellationToken)
    {
        if (breakdown == ReportBreakdown.ByAccount)
        {
            return await _reportService.GetAccountBreakdownReportAsync(filters, cancellationToken)
                .Select(static x => ReportRows.From(x))
                .ToListAsync(cancellationToken);
        }

        if (breakdown == ReportBreakdown.ByTag)
        {
            return await _reportService.GetCategoryBreakdownReportAsync(filters, cancellationToken)
                .Select(static x => ReportRows.From(x))
                .ToListAsync(cancellationToken);
        }

        return await _reportService.GetSpentInTimePeriodReportAsync(filters, cancellationToken)
            .Select(static x => ReportRows.From(x))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Each filter chosen apart from the dates, by the names the filter bar shows, e.g. Accounts: Main, Savings.
    /// </summary>
    private async Task<List<string>> DescribeAsync(FilterTransactions filters, CancellationToken cancellationToken)
    {
        List<string> described = [];

        if (filters.AccountIds is { Count: > 0 } accountIds)
        {
            List<string> names = await _transactionsService.GetAccountsForTransactionFiltersAsync(cancellationToken)
                .Where(x => accountIds.Contains(x.AccountId))
                .Select(static x => x.AccountName)
                .ToListAsync(cancellationToken);
            described.Add(Named("Accounts", names));
        }

        if (filters.ProviderIds is { Count: > 0 } providerIds)
        {
            List<string> names = await _transactionsService.GetProvidersForTransactionFiltersAsync(cancellationToken)
                .Where(x => providerIds.Contains(x.ProviderId))
                .Select(static x => x.ProviderName)
                .ToListAsync(cancellationToken);
            described.Add(Named("Banks", names));
        }

        if (filters.Tags is { Count: > 0 } tags)
        {
            described.Add(Named("Tags", tags));
        }

        if (filters.Categories is { Count: > 0 } categories)
        {
            described.Add(Named("Categories", categories));
        }

        if (filters.Types is { Count: > 0 } types)
        {
            described.Add(Named("Types", types));
        }

        if (filters.BillIds is { Count: > 0 } billIds)
        {
            List<string> names = await _billsService.GetBillsAsync(cancellationToken)
                .Where(x => billIds.Contains(x.Id))
                .Select(static x => x.Name)
                .ToListAsync(cancellationToken);
            described.Add(Named("Bills", names));
        }

        if (filters.DebtIds is { Count: > 0 } debtIds)
        {
            List<string> names = await _debtsService.GetDebtsAsync(cancellationToken)
                .Where(x => debtIds.Contains(x.Id))
                .Select(static x => x.Name)
                .ToListAsync(cancellationToken);
            described.Add(Named("Debts", names));
        }

        if (filters.SavingsGoalIds is { Count: > 0 } goalIds)
        {
            List<string> names = await _savingsGoalsService.GetSavingsGoalsAsync(cancellationToken)
                .Where(x => goalIds.Contains(x.Id))
                .Select(static x => x.Name)
                .ToListAsync(cancellationToken);
            described.Add(Named("Savings goals", names));
        }

        if (!string.IsNullOrWhiteSpace(filters.SearchTerm))
        {
            described.Add($"Description contains “{filters.SearchTerm.Trim()}”");
        }

        return described;
    }

    private static string Named(string filter, IEnumerable<string> names) => $"{filter}: {string.Join(", ", names)}";

    private static DateOnly? Day(DateTime? date)
    {
        if (date is null)
        {
            return null;
        }

        return DateOnly.FromDateTime(date.Value);
    }
}
