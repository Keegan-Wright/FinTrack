using FinanceTracker.Contracts.Calendar;

namespace FinanceTracker.Infrastructure.Calendar;

public interface ICalendarService
{
    IAsyncEnumerable<CalendarItems>
        GetMonthItemsAsync(int month, int year, CancellationToken cancellationToken);
}
