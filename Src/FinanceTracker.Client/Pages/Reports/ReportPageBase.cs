using System.Collections.Immutable;
using System.Globalization;
using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceTracker.Client.Pages.Reports;

/// <summary>
/// Base for the report pages. Loads the filter options on start, runs the report for the chosen filters, and
/// helpers for charting and comparing its breakdowns.
/// </summary>
/// <typeparam name="TReportResponse">The report the page shows, one of the <see cref="IReportService"/> results.</typeparam>
public class ReportPageBase<TReportResponse> : PageComponent
{
    internal readonly List<TransactionAccountFilter> _accountFilterItems = [];
    internal readonly List<TransactionCategoryFilter> _categoryFilterItems = [];

    internal readonly ChartOptions _chartOptions = new();
    internal readonly List<TransactionProviderFilter> _providerFilterItems = [];

    internal readonly string _searchTerm = string.Empty;
    internal readonly List<TransactionTagFilter> _transactionTagFilterItems = [];
    internal readonly List<TransactionTypeFilter> _transactionTypeFilterItems = [];

    [Inject]
    private ITransactionsService TransactionsService { get; set; } = null!;

    [Inject]
    private IReportService ReportService { get; set; } = null!;

    private IAsyncEnumerable<TReportResponse>? ReportResponseEnumerable { get; set; }
    /// <summary>
    /// The report's results for the current filters, filled by <see cref="LoadReportAsync"/>.
    /// </summary>
    internal List<TReportResponse> ReportItems { get; } = [];


    /// <summary>
    /// Runs <paramref name="reportType"/> for <paramref name="filters"/>, replacing <see cref="ReportItems"/>.
    /// </summary>
    /// <param name="filters">The filters chosen in the filter bar.</param>
    /// <param name="reportType">Must match <typeparamref name="TReportResponse"/>, or the cast fails.</param>
    internal async Task LoadReportAsync(TransactionFilters.ActiveTransactionFilters filters, ReportType reportType)
    {
        SetLoadingState(true, "Loading Report");
        ReportItems.Clear();
        BaseReport report = new()
        {
            AccountIds = filters.AccountIds.ToImmutableList(),
            ProviderIds = filters.ProviderIds.ToImmutableList(),
            Categories = filters.Categories.ToImmutableList(),
            Types = filters.TransactionTypes.ToImmutableList(),
            SearchTerm = filters.SearchTerm,
            FromDate = filters.DateRange?.Start,
            ToDate = filters.DateRange?.End,
            Tags = filters.TransactionTags.ToImmutableList(),
            SyncTypes = SyncTypes.All
        };

        ReportResponseEnumerable = reportType switch
        {
            ReportType.SpentInTimePeriod => (IAsyncEnumerable<TReportResponse>)ReportService
                .GetSpentInTimePeriodReportAsync(report, _cts.Token),
            ReportType.CategoryBreakdown => (IAsyncEnumerable<TReportResponse>)ReportService
                .GetCategoryBreakdownReportAsync(report, _cts.Token),
            ReportType.AccountBreakdown => (IAsyncEnumerable<TReportResponse>)ReportService
                .GetAccountBreakdownReportAsync(report, _cts.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(reportType), reportType, null)
        };

        await foreach (TReportResponse reportItem in ReportResponseEnumerable.WithCancellation(_cts.Token))
        {
            ReportItems.Add(reportItem);
        }

        SetLoadingState(false);
    }

    private async Task LoadFilterItemsAsync()
    {
        await foreach (TransactionAccountFilter account in TransactionsService
                           .GetAccountsForTransactionFiltersAsync(SyncTypes.All, _cts.Token))
        {
            _accountFilterItems.Add(account);
        }

        await foreach (TransactionCategoryFilter category in TransactionsService
                           .GetCategoriesForTransactionFiltersAsync(_cts.Token))
        {
            _categoryFilterItems.Add(category);
        }

        await foreach (TransactionProviderFilter provider in TransactionsService
                           .GetProvidersForTransactionFiltersAsync(_cts.Token))
        {
            _providerFilterItems.Add(provider);
        }

        await foreach (TransactionTypeFilter type in
                       TransactionsService.GetTypesForTransactionFiltersAsync(_cts.Token))
        {
            _transactionTypeFilterItems.Add(type);
        }

        await foreach (TransactionTagFilter tag in
                       TransactionsService.GetTagsForTransactionFiltersAsync(_cts.Token))
        {
            _transactionTagFilterItems.Add(tag);
        }
    }


    internal static List<ChartSeries<double>> GetMonthlyGraphSeries<T>(IEnumerable<T> reportItems) where T : IReport
    {
        ChartSeries<double> totalIn = new()
        {
            Name = "Total In", Data = reportItems.Select(x => Convert.ToDouble(x.TotalIn)).ToArray()
        };
        ChartSeries<double> totalOut = new()
        {
            Name = "Total Out", Data = reportItems.Select(x => Convert.ToDouble(x.TotalOut)).ToArray()
        };
        ChartSeries<double> totalDif = new()
        {
            Name = "Total Dif",
            Data = reportItems.Select(x => Convert.ToDouble(x.TotalOut) + Convert.ToDouble(x.TotalIn)).ToArray()
        };
        ChartSeries<double> transactions = new()
        {
            Name = "Transactions Made",
            Data = reportItems.Select(x => Convert.ToDouble(x.TotalTransactions)).ToArray()
        };

        return [totalIn, totalOut, totalDif, transactions];
    }

    internal static List<ChartSeries<double>> GetDailyGraphSeries<T>(IEnumerable<T> reportItems) where T : IReport
    {
        ChartSeries<double> totalIn = new()
        {
            Name = "Total In", Data = reportItems.Select(x => Convert.ToDouble(x.TotalIn)).ToArray()
        };
        ChartSeries<double> totalOut = new()
        {
            Name = "Total Out", Data = reportItems.Select(x => Convert.ToDouble(x.TotalOut)).ToArray()
        };
        ChartSeries<double> totalDif = new()
        {
            Name = "Total Dif",
            Data = reportItems.Select(x => Convert.ToDouble(x.TotalOut) + Convert.ToDouble(x.TotalIn)).ToArray()
        };
        ChartSeries<double> transactions = new()
        {
            Name = "Transactions Made",
            Data = reportItems.Select(x => Convert.ToDouble(x.TotalTransactions)).ToArray()
        };

        return [totalIn, totalOut, totalDif, transactions];
    }

    internal static string[] GetMonthlyGraphAxis() => DateTimeFormatInfo.CurrentInfo.MonthNames;

    internal static string[] GetDailyGraphAxis(int year, int month)
    {
        int days = DateTime.DaysInMonth(year, month);

        string[] enumerable = Enumerable.Range(1, days + 1).Select(x => x.ToString(CultureInfo.CurrentCulture)).ToArray();
        return enumerable;
    }

    internal static decimal CalculatePerDayAverage(decimal total, int daysInPeriod) =>
        daysInPeriod > 0 ? total / daysInPeriod : 0;

    /// <summary>
    /// The change from <paramref name="previous"/> to <paramref name="current"/>. With nothing to compare against,
    /// just the direction: 1, -1 or 0.
    /// </summary>
    internal static decimal? CalculateTrendComparison(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return current > 0 ? 1 : current < 0 ? -1 : 0;
        }

        return current - previous;
    }

    /// <summary>
    /// The change from <paramref name="previous"/> to <paramref name="current"/> as a fraction of
    /// <paramref name="previous"/> (0.1 is 10%), or <see langword="null"/> when <paramref name="previous"/> is 0.
    /// </summary>
    internal static decimal? CalculateTrendPercentage(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return null;
        }

        return (current - previous) / Math.Abs(previous);
    }

    internal static int GetDaysInMonth(int year, int month) => DateTime.DaysInMonth(year, month);

    internal static int GetDaysInYear(int year) => DateTime.IsLeapYear(year) ? 366 : 365;

    /// <summary>
    /// The totals of the breakdown before <paramref name="currentMonthIndex"/>, or zeros for the first.
    /// </summary>
    internal static (decimal TotalIn, decimal TotalOut) GetPreviousPeriodTotals<T>(
        IEnumerable<T> previous,
        int currentMonthIndex) where T : IReport
    {
        if (currentMonthIndex <= 0)
        {
            return (0, 0);
        }

        T previousMonth = previous.ElementAt(currentMonthIndex - 1);
        return (previousMonth.TotalIn, previousMonth.TotalOut);
    }

    internal static string GetYearTabLabel(int year, int totalTransactions) => $"{year} ({totalTransactions} transactions)";

    internal static string GetMonthPanelLabel(string month, int totalTransactions, int daysInMonth) =>
        $"{month} ({totalTransactions} transactions, {daysInMonth} days)";

    internal static int GetMonthIndex(string monthName)
    {
        int monthIndex = Array.IndexOf(DateTimeFormatInfo.CurrentInfo.MonthNames, monthName);
        return monthIndex >= 0 ? monthIndex + 1 : 1;
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        SetLoadingState(true, "Loading filters");
        await LoadFilterItemsAsync();
        SetLoadingState(false);
    }
}
