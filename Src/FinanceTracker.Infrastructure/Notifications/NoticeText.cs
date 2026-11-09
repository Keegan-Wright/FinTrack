using System.Globalization;

namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Amounts and days as notifications say them, in the app's culture, e.g. <c>£12.50</c> and <c>14 November</c>.
/// </summary>
public static class NoticeText
{
    public static string Money(decimal amount) => amount.ToString("C", CultureInfo.CurrentCulture);

    public static string Day(DateOnly day) => day.ToString("d MMMM", CultureInfo.CurrentCulture);

    /// <summary>
    /// When something is due from <paramref name="today"/>, e.g. <c>today</c>, <c>tomorrow</c> or
    /// <c>in 3 days</c>.
    /// </summary>
    public static string DueIn(DateOnly due, DateOnly today)
    {
        int days = due.DayNumber - today.DayNumber;
        if (days <= 0)
        {
            return "today";
        }

        if (days == 1)
        {
            return "tomorrow";
        }

        return $"in {days} days";
    }
}
