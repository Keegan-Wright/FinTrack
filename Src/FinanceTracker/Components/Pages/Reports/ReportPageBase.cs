using FinanceTracker.Components.Transactions;
using FinanceTracker.Enums;
using FinanceTracker.Models.Request.Transaction;
using FinanceTracker.Models.Response.Transaction;
using FinanceTracker.Services.Transactions;
using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Components.Pages.Reports;

public class ReportPageBase : PageComponent
{
    [Inject] private ITransactionsService TransactionsService { get; set; }
    
    internal readonly List<TransactionAccountFilterResponse> _accountFilterItems = [];
    internal readonly List<TransactionCategoryFilterResponse> _categoryFilterItems = [];
    internal readonly List<TransactionProviderFilterResponse> _providerFilterItems = [];
    internal readonly List<TransactionTypeFilterResponse> _transactionTypeFilterItems = [];
    internal readonly List<TransactionTagFilterResponse> _transactionTagFilterItems = [];
    
    internal readonly string _searchTerm;
    
    internal async Task LoadReportAsync(TransactionFilters.ActiveTransactionFilters filters)
    {
        SetLoadingState(true, "Loading Report");

        // Placeholder for reports
        var transactionRequest = new FilteredTransactionsRequest
        {
            AccountIds = filters.AccountIds.ToList(),
            ProviderIds = filters.ProviderIds.ToList(),
            Categories = filters.Categories.ToList(),
            Types = filters.TransactionTypes.ToList(),
            SearchTerm = filters.SearchTerm,
            FromDate = filters.DateRange?.Start,
            ToDate = filters.DateRange?.End,
            Tags = filters.TransactionTags.ToList()
        };
        
        // Load Report here
        
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
}