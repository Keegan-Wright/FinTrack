using FinanceTracker.Components.Transactions;
using FinanceTracker.Enums;
using FinanceTracker.Models.Request.Reports;
using FinanceTracker.Models.Request.Transaction;
using FinanceTracker.Models.Response.Reports;
using FinanceTracker.Models.Response.Reports.Account;
using FinanceTracker.Models.Response.Transaction;
using FinanceTracker.Services.Reports;
using FinanceTracker.Services.Transactions;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Charts;
using System.Globalization;

namespace FinanceTracker.Components.Pages.Reports;

public class ReportPageBase<TReportResponse> : PageComponent

{
    [Inject] private ITransactionsService TransactionsService { get; set; }
    [Inject] private IReportService ReportService { get; set; }
    
    internal readonly List<TransactionAccountFilterResponse> _accountFilterItems = [];
    internal readonly List<TransactionCategoryFilterResponse> _categoryFilterItems = [];
    internal readonly List<TransactionProviderFilterResponse> _providerFilterItems = [];
    internal readonly List<TransactionTypeFilterResponse> _transactionTypeFilterItems = [];
    internal readonly List<TransactionTagFilterResponse> _transactionTagFilterItems = [];
    
    private IAsyncEnumerable<TReportResponse> ReportResponseEnumerable { get; set; }
    internal List<TReportResponse> ReportItems { get; set; } = [];
    
    internal readonly string _searchTerm;

    internal readonly ChartOptions _chartOptions = new ChartOptions()
    {
    };
        
        
    
    internal async Task LoadReportAsync(TransactionFilters.ActiveTransactionFilters filters, ReportType reportType)
    {
        SetLoadingState(true, "Loading Report");
        ReportItems.Clear();
        var reportRequest = new BaseReportRequest
        {
            AccountIds = filters.AccountIds.ToList(),
            ProviderIds = filters.ProviderIds.ToList(),
            Categories = filters.Categories.ToList(),
            Types = filters.TransactionTypes.ToList(),
            SearchTerm = filters.SearchTerm,
            FromDate = filters.DateRange?.Start,
            ToDate = filters.DateRange?.End,
            Tags = filters.TransactionTags.ToList(),
            SyncTypes = SyncTypes.All
        };

        switch (reportType)
        {
            case ReportType.SpentInTimePeriod:
                ReportResponseEnumerable = (IAsyncEnumerable<TReportResponse>)ReportService.GetSpentInTimePeriodReportAsync(reportRequest, Cts.Token);
                break;
            case ReportType.CategoryBreakdown:
                ReportResponseEnumerable = (IAsyncEnumerable<TReportResponse>)ReportService.GetCategoryBreakdownReportAsync(reportRequest, Cts.Token);
                break;
            case ReportType.AccountBreakdown:
                ReportResponseEnumerable = (IAsyncEnumerable<TReportResponse>)ReportService.GetAccountBreakdownReportAsync(reportRequest, Cts.Token);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reportType), reportType, null);
        }


        await foreach (var reportItem in ReportResponseEnumerable.WithCancellation(Cts.Token))
        {
            ReportItems.Add(reportItem);
        }
        
        SetLoadingState(false);
    }
    
    internal async Task LoadFilterItemsAsync()
    {
        await foreach (var account in TransactionsService.GetAccountsForTransactionFiltersAsync(SyncTypes.All, Cts.Token))
        {
            _accountFilterItems.Add(account);
        }

        await foreach (var category in TransactionsService.GetCategoriesForTransactionFiltersAsync(Cts.Token))
        {
            _categoryFilterItems.Add(category);
        }

        await foreach (var provider in TransactionsService.GetProvidersForTransactionFiltersAsync(Cts.Token))
        {
            _providerFilterItems.Add(provider);
        }
        
        await foreach (var type in TransactionsService.GetTypesForTransactionFiltersAsync(Cts.Token))
        {
            _transactionTypeFilterItems.Add(type);
        }    
        
        await foreach (var tag in TransactionsService.GetTagsForTransactionFiltersAsync(Cts.Token))
        {
            _transactionTagFilterItems.Add(tag);
        }
    }



    internal List<ChartSeries<double>> GetMonthlyGraphSeries(List<SharedReportResponse> reportItems)
    {

        var totalIn = new ChartSeries<double>()
            { Name = "Total In", Data = reportItems.Select(x => Convert.ToDouble(x.TotalIn)).ToArray() };
        var totalOut = new ChartSeries<double>()
            { Name = "Total Out", Data = reportItems.Select(x => Convert.ToDouble(x.TotalOut)).ToArray() };
        var totalDif = new ChartSeries<double>()
            { Name = "Total Dif", Data = reportItems.Select(x => Convert.ToDouble(x.TotalOut) + Convert.ToDouble(x.TotalIn)).ToArray() };
        var transactions = new ChartSeries<double>()
            { Name = "Transactions Made", Data = reportItems.Select(x => Convert.ToDouble(x.TotalTransactions)).ToArray() };
        
        return [totalIn, totalOut, totalDif, transactions];
    }
    
    internal List<ChartSeries<double>> GetDailyGraphSeries(List<SharedReportResponse> reportItems)
    {

        var totalIn = new ChartSeries<double>()
            { Name = "Total In", Data = reportItems.Select(x => Convert.ToDouble(x.TotalIn)).ToArray() };
        var totalOut = new ChartSeries<double>()
            { Name = "Total Out", Data = reportItems.Select(x => Convert.ToDouble(x.TotalOut)).ToArray() };
        var totalDif = new ChartSeries<double>()
            { Name = "Total Dif", Data = reportItems.Select(x => Convert.ToDouble(x.TotalOut) + Convert.ToDouble(x.TotalIn)).ToArray() };
        var transactions = new ChartSeries<double>()
            { Name = "Transactions Made", Data = reportItems.Select(x => Convert.ToDouble(x.TotalTransactions)).ToArray() };
        
        return [totalIn, totalOut, totalDif, transactions];
    }

    internal string[] GetMonthlyGraphAxis()
    { 
        return DateTimeFormatInfo.CurrentInfo.MonthNames;
    }
    
    internal string[] GetDailyGraphAxis(int year, int month)
    { 
        var days =  DateTime.DaysInMonth(year, month);
        
        var enumerable = Enumerable.Range(1, days + 1).Select(x => x.ToString()).ToArray();
        return enumerable;
    }

    internal decimal CalculatePerDayAverage(decimal total, int daysInPeriod)
    {
        return daysInPeriod > 0 ? total / daysInPeriod : 0;
    }

    internal decimal? CalculateTrendComparison(decimal current, decimal previous)
    {
        if (previous == 0)
            return current > 0 ? 1 : (current < 0 ? -1 : 0);

        return current - previous;
    }

    internal decimal? CalculateTrendPercentage(decimal current, decimal previous)
    {
        if (previous == 0)
            return null;

        return (current - previous) / Math.Abs(previous);
    }

    internal int GetDaysInMonth(int year, int month)
    {
        return DateTime.DaysInMonth(year, month);
    }

    internal int GetDaysInYear(int year)
    {
        return DateTime.IsLeapYear(year) ? 366 : 365;
    }

    internal (decimal TotalIn, decimal TotalOut) GetPreviousPeriodTotals(
        List<SharedReportResponse> previous,
        int currentMonthIndex)
    {
        if (currentMonthIndex <= 0)
            return (0, 0);

        var previousMonth = previous[currentMonthIndex - 1];
        return (previousMonth.TotalIn, previousMonth.TotalOut);
    }

    internal string GetYearTabLabel(int year, int totalTransactions)
    {
        return $"{year} ({totalTransactions} transactions)";
    }

    internal string GetMonthPanelLabel(string month, int totalTransactions, int daysInMonth)
    {
        return $"{month} ({totalTransactions} transactions, {daysInMonth} days)";
    }

    internal int GetMonthIndex(string monthName)
    {
        var monthIndex = Array.IndexOf(DateTimeFormatInfo.CurrentInfo.MonthNames, monthName);
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