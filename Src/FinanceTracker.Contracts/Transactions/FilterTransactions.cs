using System.Collections.Immutable;
using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// Which transactions to list or report on, shared by the transactions page and every report. Every filter is
/// optional, empty matches everything.
/// </summary>
public class FilterTransactions
{
    /// <summary>
    /// Only these accounts.
    /// </summary>
    [Description("List of account identifiers to filter transactions")]
    public IImmutableList<Guid>? AccountIds { get; init; } = [];

    /// <summary>
    /// Only these bank connections, see <see cref="TransactionProviderFilter.ProviderId"/>.
    /// </summary>
    [Description("List of banking provider identifiers to filter transactions")]
    public IImmutableList<Guid>? ProviderIds { get; init; } = [];

    /// <summary>
    /// Only these provider categories, matched exactly.
    /// </summary>
    [Description("List of transaction categories to filter transactions")]
    public IImmutableList<string>? Categories { get; init; } = [];

    /// <summary>
    /// Only these transaction types, <c>Debit</c> or <c>Credit</c>.
    /// <remarks>Casing ignored.</remarks>
    /// </summary>
    [Description("List of transaction types to filter transactions")]
    public IImmutableList<string>? Types { get; init; } = [];

    /// <summary>
    /// Only transactions whose description contains this.
    /// <remarks>Casing ignored.</remarks>
    /// </summary>
    [Description("Search term to filter transactions by description or other text fields")]
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Only transactions at or after this time.
    /// </summary>
    [Description("Start date for filtering transactions")]
    public DateTime? FromDate { get; init; }

    /// <summary>
    /// Only transactions at or before this time.
    /// </summary>
    [Description("End date for filtering transactions")]
    public DateTime? ToDate { get; init; }

    /// <summary>
    /// Only transactions with at least one of these tags (provider or custom classifications).
    /// </summary>
    [Description("List of tags to filter transactions")]
    public IImmutableList<string>? Tags { get; init; } = [];
}
