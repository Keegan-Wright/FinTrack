using System.Collections.Immutable;
using System.ComponentModel;

namespace FinanceTracker.Contracts.Reports.Category;

public class SpentInCategoryReportMonthlyBreakdownResponse : SharedReportResponse
{
    [Description("Month number (1-12) for which the spending breakdown is provided")]
    public required string Month { get; init; }

    [Description("Daily breakdown of spending for the specified month")]
    public IImmutableList<SpentInCategoryReportDailyBreakdownResponse> DailyBreakdown { get; } = [];
}
