using FinanceTracker.Models.Response.Calendar;

namespace FinanceTracker.Services.Calendar;

public interface ICalendarService
{
    IAsyncEnumerable<CalendarItemsResponse>
        GetMonthItemsAsync(int month, int year, CancellationToken cancellationToken);
}
