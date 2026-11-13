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
    /// Something's name, saying whose when another person shares it, e.g. <c>Rent</c> or <c>Jane Doe's Rent</c>.
    /// </summary>
    /// <param name="sharedBy">Its owner's name when it's someone else's, null when it's the user's.</param>
    public static string Named(string name, string? sharedBy)
    {
        if (string.IsNullOrEmpty(sharedBy))
        {
            return name;
        }

        return $"{sharedBy}'s {name}";
    }

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
