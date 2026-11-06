namespace FinanceTracker.Client.Components.Common;

/// <summary>
/// The instants whole days where the user is run between, in UTC, for the date filters the server compares with
/// when transactions happened. A day in summer time in the UK starts at 23:00 UTC the day before, so a payment just
/// after midnight belongs to the day it was where the user is.
/// </summary>
public static class LocalDays
{
    /// <summary>
    /// Midnight at the start of <paramref name="day"/>'s date where the user is, as UTC.
    /// </summary>
    public static DateTime StartUtc(DateTime day, TimeZoneInfo timeZone)
    {
        DateTime midnight = DateTime.SpecifyKind(day.Date, DateTimeKind.Unspecified);

        // A zone that moves its clocks at midnight skips it, the day starts once the clocks have gone forward
        while (timeZone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddMinutes(30);
        }

        return TimeZoneInfo.ConvertTimeToUtc(midnight, timeZone);
    }

    /// <summary>
    /// The last tick of <paramref name="day"/>'s date where the user is, as UTC.
    /// </summary>
    public static DateTime EndUtc(DateTime day, TimeZoneInfo timeZone) =>
        StartUtc(day.Date.AddDays(1), timeZone).AddTicks(-1);
}
