using FinanceTracker.Contracts.Calendar;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's transactions and savings goal dates laid out by day, for the calendar page.
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// One <see cref="CalendarItems"/> per day of the month, in date order. Transfers between the user's own accounts
    /// or within their household aren't income or spending, so aren't included.
    /// </summary>
    /// <param name="month">The month, 1 to 12.</param>
    /// <param name="year">The year.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <remarks>
    /// Dates are encrypted, so every transaction is loaded and filtered after
    /// decryption.
    /// </remarks>
    IAsyncEnumerable<CalendarItems> GetMonthItemsAsync(int month, int year, CancellationToken cancellationToken);
}
