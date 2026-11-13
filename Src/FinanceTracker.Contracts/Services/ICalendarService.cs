using FinanceTracker.Contracts.Calendar;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The user's calendar transactions, bills, savings goal and debt dates, and their own events, laid out by
/// day.
/// </summary>
/// <remarks>
/// Methods that look up an event by id throw <see cref="InvalidOperationException"/> when it doesn't exist or belongs
/// to another user.
/// </remarks>
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

    /// <summary>
    /// All of the user's events, by the next day each is on, passed one-offs last.
    /// </summary>
    IAsyncEnumerable<CalendarEvent> GetEventsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds an event, creating its savings goal when asked. Validate the request with
    /// <see cref="Validation.SaveCalendarEventValidator"/> first.
    /// </summary>
    /// <returns>The event as stored, with its new id.</returns>
    /// <exception cref="InvalidOperationException">The savings goal isn't the user's.</exception>
    /// <exception cref="NameTakenException">It creates a goal, and the user already has a goal called its title.
    /// </exception>
    Task<CalendarEvent> AddEventAsync(SaveCalendarEvent calendarEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Changes one of the user's events, creating its savings goal when asked. Validate the request with
    /// <see cref="Validation.SaveCalendarEventValidator"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no event with <paramref name="id"/>, or the savings
    /// goal isn't theirs.</exception>
    /// <exception cref="NameTakenException">It creates a goal, and the user already has a goal called its title.
    /// </exception>
    Task<CalendarEvent> UpdateEventAsync(Guid id, SaveCalendarEvent calendarEvent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's events. Its savings goal stays.
    /// </summary>
    /// <exception cref="NotFoundException">The user has no event with <paramref name="id"/>.</exception>
    Task DeleteEventAsync(Guid id, CancellationToken cancellationToken);
}
