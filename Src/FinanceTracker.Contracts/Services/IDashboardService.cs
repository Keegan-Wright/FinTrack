using FinanceTracker.Contracts.Dashboard;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// Totals and upcoming payments for the signed in user's dashboard.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Money in and out across all the user's accounts between two dates. Transfers between their own accounts or
    /// within their household are left out.
    /// </summary>
    /// <param name="fromDate">The start of the period, included.</param>
    /// <param name="toDate">The end of the period, left out, so back to back periods never share a transaction.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    Task<SpentInTimePeriod> GetSpentInTimePeriod(DateTime fromDate, DateTime toDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// The user's bill payments due from a week ago on, soonest first, so those just paid show ticked off and late
    /// ones show until they're paid.
    /// </summary>
    /// <param name="numberToFetch">The most payments to return.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    IAsyncEnumerable<UpcomingPayment> GetUpcomingPaymentsAsync(int numberToFetch,
        CancellationToken cancellationToken);
}
