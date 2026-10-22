using System.Collections.Immutable;
using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports;

/// <summary>
/// A report request which transactions to report on. Every filter is optional, empty matches everything.
/// </summary>
public class BaseReport
{
    /// <summary>
    /// Only these accounts.
    /// </summary>
    [Description("List of account identifiers to include in the report")]
    public IImmutableList<Guid>? AccountIds { get; init; }

    /// <summary>
    /// Only these bank connections, see <see cref="Transactions.TransactionProviderFilter.ProviderId"/>.
    /// </summary>
    [Description("List of banking provider identifiers to include in the report")]
    public IImmutableList<Guid>? ProviderIds { get; init; }

    /// <summary>
    /// Only transactions at or after this time.
    /// </summary>
    [Description("Start date for the report period")]
    public DateTime? FromDate { get; init; }

    /// <summary>
    /// Only transactions at or before this time.
    /// </summary>
    [Description("End date for the report period")]
    public DateTime? ToDate { get; init; }

    /// <summary>
    /// Only these transaction types, <c>Debit</c> or <c>Credit</c>.
    /// <remarks>Casing ignored.</remarks>
    /// </summary>
    [Description("List of transaction types to include in the report")]
    public IImmutableList<string>? Types { get; init; }

    /// <summary>
    /// Only these provider categories.
    /// </summary>
    [Description("List of transaction categories to include in the report")]
    public IImmutableList<string>? Categories { get; init; }


    /// <summary>
    /// Not used yet - likely remove.
    /// </summary>
    [Description("Type of synchronization to perform")]
    public required SyncTypes SyncTypes { get; init; }

    /// <summary>
    /// Only transactions with at least one of these tags (provider or custom classifications).
    /// </summary>
    [Description("List of tags to filter the report")]
    public IImmutableList<string>? Tags { get; init; } = [];

    /// <summary>
    /// Only transactions whose description contains this.
    /// <remarks>Casing ignored.</remarks>
    /// </summary>
    [Description("Search term to filter transactions by description or other text fields")]
    public string? SearchTerm { get; init; }
}
