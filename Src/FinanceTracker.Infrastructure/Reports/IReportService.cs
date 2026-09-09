using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;

namespace FinanceTracker.Infrastructure.Reports;

public interface IReportService
{
    IAsyncEnumerable<SpentInTimePeriodReportResponse> GetSpentInTimePeriodReportAsync(BaseReportRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SpentInCategoryReportResponse> GetCategoryBreakdownReportAsync(BaseReportRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SpentInAccountReportResponse> GetAccountBreakdownReportAsync(BaseReportRequest request,
        CancellationToken cancellationToken);
}
