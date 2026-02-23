using FinanceTracker.Models.Request.Reports;
using FinanceTracker.Models.Response.Reports.Account;
using FinanceTracker.Models.Response.Reports.Category;
using FinanceTracker.Models.Response.Reports.SpentInTimePeriod;

namespace FinanceTracker.Services.Reports;

public interface IReportService
{
    IAsyncEnumerable<SpentInTimePeriodReportResponse> GetSpentInTimePeriodReportAsync(BaseReportRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SpentInCategoryReportResponse> GetCategoryBreakdownReportAsync(BaseReportRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SpentInAccountReportResponse> GetAccountBreakdownReportAsync(BaseReportRequest request,
        CancellationToken cancellationToken);
}
