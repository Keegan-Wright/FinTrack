using FinanceTracker.Domain.PayPeriods;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.PayPeriods;

/// <summary>
/// A user's pay periods, for the services that work by month, see <see cref="PayCalendar"/>.
/// </summary>
internal static class PayCalendars
{
    /// <summary>
    /// From pay to pay, or calendar months until a pay is found or while the user has them turned off.
    /// </summary>
    public static async Task<PayCalendar> ForUserAsync(FinanceTrackerContext context, Guid userId, DateOnly today,
        CancellationToken cancellationToken)
    {
        PaySchedule? schedule = await context.PaySchedules.OwnedBy(userId).AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (schedule is null)
        {
            return PayCalendar.CalendarMonths;
        }

        return schedule.Calendar(today);
    }
}
