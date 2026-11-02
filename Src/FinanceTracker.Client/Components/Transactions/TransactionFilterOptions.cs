using FinanceTracker.Client.Components.Bills;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// The values the transaction filters offer, shared by the transactions page and the reports. Persisted from the
/// prerender as one value, see <see cref="Pages.PageComponent.LoadOnceAsync{T}"/>.
/// </summary>
public sealed class TransactionFilterOptions
{
    /// <summary>
    /// No options, for the filters to show until the real ones load.
    /// </summary>
    public static TransactionFilterOptions None { get; } = new();

    public List<TransactionAccountFilter> Accounts { get; init; } = [];
    public List<TransactionCategoryFilter> Categories { get; init; } = [];
    public List<TransactionProviderFilter> Providers { get; init; } = [];
    public List<TransactionTypeFilter> Types { get; init; } = [];
    public List<TransactionTagFilter> Tags { get; init; } = [];
    public List<TransactionLinkFilter> Bills { get; init; } = [];
    public List<TransactionLinkFilter> Debts { get; init; } = [];
    public List<TransactionLinkFilter> SavingsGoals { get; init; } = [];

    public static async Task<TransactionFilterOptions> LoadAsync(ITransactionsService transactionsService,
        IBillsService billsService, IDebtsService debtsService, ISavingsGoalsService savingsGoalsService,
        CancellationToken cancellationToken) => new()
    {
        Accounts = await transactionsService.GetAccountsForTransactionFiltersAsync(cancellationToken)
            .ToListAsync(cancellationToken),
        Categories = await transactionsService.GetCategoriesForTransactionFiltersAsync(cancellationToken)
            .ToListAsync(cancellationToken),
        Providers = await transactionsService.GetProvidersForTransactionFiltersAsync(cancellationToken)
            .ToListAsync(cancellationToken),
        Types = await transactionsService.GetTypesForTransactionFiltersAsync(cancellationToken)
            .ToListAsync(cancellationToken),
        Tags = await transactionsService.GetTagsForTransactionFiltersAsync(cancellationToken)
            .ToListAsync(cancellationToken),
        Bills = await billsService.GetBillsAsync(cancellationToken)
            .OrderBy(static x => x.Name)
            .Select(static x => new TransactionLinkFilter(x.Id, BillText.PickerLabel(x)))
            .ToListAsync(cancellationToken),
        Debts = await debtsService.GetDebtsAsync(cancellationToken)
            .OrderBy(static x => x.Name)
            .Select(static x => new TransactionLinkFilter(x.Id, x.Name))
            .ToListAsync(cancellationToken),
        SavingsGoals = await savingsGoalsService.GetSavingsGoalsAsync(cancellationToken)
            .OrderBy(static x => x.Name)
            .Select(static x => new TransactionLinkFilter(x.Id, x.Name))
            .ToListAsync(cancellationToken)
    };
}
