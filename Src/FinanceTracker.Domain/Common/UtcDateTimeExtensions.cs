namespace FinanceTracker.Domain.Common;

public static class UtcDateTimeExtensions
{
    extension(DateTime utc)
    {
        /// <summary>
        /// The day this UTC time falls on where the user is, so a payment just after midnight counts on that day even
        /// when it's still the day before in UTC.
        /// </summary>
        public DateOnly LocalDayIn(TimeZoneInfo timeZone)
        {
            ArgumentNullException.ThrowIfNull(timeZone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                timeZone));
        }
    }
}
