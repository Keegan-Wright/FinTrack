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

    /// <summary>
    /// The accounts to filter by.
    /// </summary>
    public List<TransactionAccountFilter> Accounts { get; init; } = [];

    /// <summary>
    /// The bank's categories to filter by.
    /// </summary>
    public List<TransactionCategoryFilter> Categories { get; init; } = [];

    /// <summary>
    /// The banks to filter by.
    /// </summary>
    public List<TransactionProviderFilter> Providers { get; init; } = [];

    /// <summary>
    /// Money in or out.
    /// </summary>
    public List<TransactionTypeFilter> Types { get; init; } = [];

    /// <summary>
    /// The user's tags to filter by.
    /// </summary>
    public List<TransactionTagFilter> Tags { get; init; } = [];

    /// <summary>
    /// The bills to filter by.
    /// </summary>
    public List<TransactionLinkFilter> Bills { get; init; } = [];

    /// <summary>
    /// The debts to filter by.
    /// </summary>
    public List<TransactionLinkFilter> Debts { get; init; } = [];

    /// <summary>
    /// The savings goals to filter by.
    /// </summary>
    public List<TransactionLinkFilter> SavingsGoals { get; init; } = [];

    /// <summary>
    /// Loads every option from the services.
    /// </summary>
    /// <param name="transactionsService">Gives the accounts, categories, banks, types and tags.</param>
    /// <param name="billsService">Gives the bills.</param>
    /// <param name="debtsService">Gives the debts.</param>
    /// <param name="savingsGoalsService">Gives the savings goals.</param>
    /// <param name="cancellationToken">Stops the loading.</param>
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
