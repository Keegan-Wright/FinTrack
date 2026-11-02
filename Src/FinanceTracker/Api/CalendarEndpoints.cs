using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class CalendarEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="ICalendarService"/> at <see cref="ApiRoutes.Calendar"/>, the user's events under
        /// <c>events</c>. Events that don't exist or aren't the user's, and goals that aren't theirs, answer 404.
        /// </summary>
        public RouteGroupBuilder MapCalendarEndpoints()
        {
            RouteGroupBuilder calendar = api.MapGroup(ApiRoutes.Calendar);

            calendar.MapGet("{year:int}/{month:int:range(1,12)}", (int year, int month, ICalendarService service,
                    CancellationToken cancellationToken) =>
                service.GetMonthItemsAsync(month, year, cancellationToken));

            calendar.MapGet("events", (ICalendarService service, CancellationToken cancellationToken) =>
                service.GetEventsAsync(cancellationToken));

            calendar.MapPost("events", async Task<Ok<CalendarEvent>> (SaveCalendarEvent request,
                        ICalendarService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.AddEventAsync(request, cancellationToken)))
                .Validated<SaveCalendarEvent>()
                .NotFoundWhenMissing();

            calendar.MapPut("events/{id:guid}", async Task<Ok<CalendarEvent>> (Guid id, SaveCalendarEvent request,
                        ICalendarService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.UpdateEventAsync(id, request, cancellationToken)))
                .Validated<SaveCalendarEvent>()
                .NotFoundWhenMissing();

            calendar.MapDelete("events/{id:guid}", async Task<NoContent> (Guid id, ICalendarService service,
                    CancellationToken cancellationToken) =>
                {
                    await service.DeleteEventAsync(id, cancellationToken);
                    return TypedResults.NoContent();
                })
                .NotFoundWhenMissing();

            return api;
        }
    }
}
