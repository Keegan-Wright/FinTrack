using FinanceTracker.Components.Transactions;
using FinanceTracker.Enums;
using FinanceTracker.Models.Request.Reports;
using FinanceTracker.Models.Request.Transaction;
using FinanceTracker.Models.Response.Transaction;
using FinanceTracker.Services.Reports;
using FinanceTracker.Services.Transactions;
using Microsoft.AspNetCore.Components;

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
    internal List<TReportResponse> ReportItems { get; set; }
    
    internal readonly string _searchTerm;
    
    internal async Task LoadReportAsync(TransactionFilters.ActiveTransactionFilters filters, ReportType reportType)
    {
        SetLoadingState(true, "Loading Report");

        // Placeholder for reports
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

    protected async override Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnInitializedAsync();
        SetLoadingState(true, "Loading filters");
        await LoadFilterItemsAsync();
        SetLoadingState(false);
    }
}