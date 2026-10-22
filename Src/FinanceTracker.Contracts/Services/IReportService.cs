using FinanceTracker.Contracts.Reports;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Spending reports over the signed in user's transactions, each broken down by year, month and day.
/// </summary>
/// <remarks>
/// Every report takes the same filters as the transactions page. Totals don't exclude transfers between the user's
/// own accounts.
/// </remarks>
public interface IReportService
{
    /// <summary>
    /// A single report covering every matching transaction, or nothing when no transactions match.
    /// </summary>
    IAsyncEnumerable<SpentInTimePeriodReport> GetSpentInTimePeriodReportAsync(BaseReport request,
        CancellationToken cancellationToken);

    /// <summary>
    /// One report per tag (provider classification or custom classification). A transaction with several tags
    /// counts towards each of them.
    /// </summary>
    IAsyncEnumerable<SpentInCategoryReport> GetCategoryBreakdownReportAsync(BaseReport request,
        CancellationToken cancellationToken);

    /// <summary>
    /// One report per account that has matching transactions.
    /// </summary>
    IAsyncEnumerable<SpentInAccountReport> GetAccountBreakdownReportAsync(BaseReport request,
        CancellationToken cancellationToken);
}
