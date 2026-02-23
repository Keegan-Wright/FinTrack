using System.Collections.Immutable;
using System.ComponentModel;

namespace FinanceTracker.Models.Request.Transaction;

public class FilteredTransactionsRequest
{
    [Description("List of account identifiers to filter transactions")]
    public IImmutableList<Guid>? AccountIds { get; init; } = [];

    [Description("List of banking provider identifiers to filter transactions")]
    public IImmutableList<Guid>? ProviderIds { get; init; } = [];

    [Description("List of transaction categories to filter transactions")]
    public IImmutableList<string>? Categories { get; init; } = [];

    [Description("List of transaction types to filter transactions")]
    public IImmutableList<string>? Types { get; init; } = [];

    [Description("Search term to filter transactions by description or other text fields")]
    public string? SearchTerm { get; init; }

    [Description("Start date for filtering transactions")]
    public DateTime? FromDate { get; init; }

    [Description("End date for filtering transactions")]
    public DateTime? ToDate { get; init; }

    [Description("List of tags to filter transactions")]
    public IImmutableList<string>? Tags { get; init; } = [];
}
