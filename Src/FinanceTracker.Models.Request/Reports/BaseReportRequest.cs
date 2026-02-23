using System.Collections.Immutable;
using System.ComponentModel;
using FinanceTracker.Enums;

namespace FinanceTracker.Models.Request.Reports;

public class BaseReportRequest
{
    [Description("List of account identifiers to include in the report")]
    public IImmutableList<Guid>? AccountIds { get; init; }

    [Description("List of banking provider identifiers to include in the report")]
    public IImmutableList<Guid>? ProviderIds { get; init; }

    [Description("Start date for the report period")]
    public DateTime? FromDate { get; init; }

    [Description("End date for the report period")]
    public DateTime? ToDate { get; init; }

    [Description("List of transaction types to include in the report")]
    public IImmutableList<string>? Types { get; init; }

    [Description("List of transaction categories to include in the report")]
    public IImmutableList<string>? Categories { get; init; }


    [Description("Type of synchronization to perform")]
    public required SyncTypes SyncTypes { get; init; }

    [Description("List of tags to filter the report")]
    public IImmutableList<string>? Tags { get; init; } = [];

    [Description("Search term to filter transactions by description or other text fields")]
    public string? SearchTerm { get; init; }
}
