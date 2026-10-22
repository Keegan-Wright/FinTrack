using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Api;

public static class CalendarEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="ICalendarService"/> at <see cref="ApiRoutes.Calendar"/>.
        /// </summary>
        public RouteGroupBuilder MapCalendarEndpoints()
        {
            RouteGroupBuilder calendar = api.MapGroup(ApiRoutes.Calendar);

            calendar.MapGet("{year:int}/{month:int:range(1,12)}", (int year, int month, ICalendarService service,
                    CancellationToken cancellationToken) =>
                service.GetMonthItemsAsync(month, year, cancellationToken));

            return api;
        }
    }
}
