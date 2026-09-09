using FinanceTracker.Contracts.Dashboard;

namespace FinanceTracker.Infrastructure.Dashboard;

public interface IDashboardService
{
    Task<SpentInTimePeriod> GetSpentInTimePeriod(DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken);

    IAsyncEnumerable<UpcomingPayment> GetUpcomingPaymentsAsync(int numberToFetch,
        CancellationToken cancellationToken);
}
