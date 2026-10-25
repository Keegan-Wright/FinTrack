using System.Collections.Immutable;
using FinanceTracker.Contracts.Transactions;
using MudBlazor;

namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// The filters chosen in <see cref="TransactionFilters"/>.
/// </summary>
public sealed record ActiveTransactionFilters(
    IEnumerable<Guid> ProviderIds,
    IEnumerable<Guid> AccountIds,
    IEnumerable<string> Categories,
    IEnumerable<string> TransactionTypes,
    IEnumerable<string> TransactionTags,
    DateRange? DateRange,
    string SearchTerm)
{
    /// <summary>
    /// Nothing filtered, every transaction.
    /// </summary>
    public static ActiveTransactionFilters None { get; } = new([], [], [], [], [], null, string.Empty);

    /// <summary>
    /// The filters as the request the transaction and report services take.
    /// </summary>
    public FilterTransactions ToRequest() => new()
    {
        AccountIds = AccountIds.ToImmutableList(),
        ProviderIds = ProviderIds.ToImmutableList(),
        Categories = Categories.ToImmutableList(),
        Types = TransactionTypes.ToImmutableList(),
        Tags = TransactionTags.ToImmutableList(),
        SearchTerm = SearchTerm,
        FromDate = DateRange?.Start?.Date,
        // The end date is a whole day, through to its last tick.
        ToDate = DateRange?.End?.Date.AddDays(1).AddTicks(-1)
    };
}
