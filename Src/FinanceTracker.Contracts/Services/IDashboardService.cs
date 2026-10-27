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
    /// The user's next standing order and direct debit payments, soonest first.
    /// </summary>
    /// <param name="numberToFetch">The most payments to return.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <remarks>
    /// Banks only report a direct debit's previous payment, so its next one is estimated as a month after it.
    /// </remarks>
    IAsyncEnumerable<UpcomingPayment> GetUpcomingPaymentsAsync(int numberToFetch,
        CancellationToken cancellationToken);
}
