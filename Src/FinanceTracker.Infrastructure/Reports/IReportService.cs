using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;

namespace FinanceTracker.Infrastructure.Reports;

public interface IReportService
{
    IAsyncEnumerable<SpentInTimePeriodReport> GetSpentInTimePeriodReportAsync(BaseReport request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SpentInCategoryReport> GetCategoryBreakdownReportAsync(BaseReport request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SpentInAccountReport> GetAccountBreakdownReportAsync(BaseReport request,
        CancellationToken cancellationToken);
}
