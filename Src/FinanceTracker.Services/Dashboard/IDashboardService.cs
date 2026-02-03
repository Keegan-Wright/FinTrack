using FinanceTracker.Models.Response.Dashboard;

namespace FinanceTracker.Services.Dashboard;

public interface IDashboardService
{
    Task<SpentInTimePeriodResponse> GetSpentInTimePeriod(DateTime from, DateTime to, CancellationToken cancellationToken);
    IAsyncEnumerable<UpcomingPaymentsResponse> GetUpcomingPaymentsAsync(int numberToFetch, CancellationToken cancellationToken);
}