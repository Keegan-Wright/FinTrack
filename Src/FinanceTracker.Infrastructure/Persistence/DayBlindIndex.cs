using System.Globalization;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// A blind index on the UTC day of an encrypted date, so a date range can be looked up as the days in it. A keyed hash
/// only for equality so ranges are turned into the list of their days.
/// </summary>
/// <remarks>
/// Rows on the same day share a hash, so the database shows which rows fall on the same day but not which day it is.
/// </remarks>
public static class DayBlindIndex
{
    /// <summary>
    /// The most days a range is looked up as. Past that the range is loaded whole.
    /// </summary>
    public const int MaxDays = 1500;

    /// <summary>
    /// How far past now a transaction can be dated.
    /// </summary>
    public const int FutureDays = 7;

    /// <summary>
    /// Days added either side of a range, for dates near midnight in another time zone.
    /// </summary>
    private const int Margin = 1;

    /// <summary>
    /// What's hashed for <paramref name="value"/>'s UTC day. Prefixed so it never shares a hash with another index's
    /// value that happens to look like a date.
    /// </summary>
    public static string Value(DateTime value) =>
        $"day:{DateOnly.FromDateTime(ToUtc(value)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// <see cref="Value"/> for every day from <paramref name="from"/> to <paramref name="to"/>, a day added either side.
    /// Null when that's more than <see cref="MaxDays"/>, or the range is backwards.
    /// </summary>
    public static List<string>? Values(DateTime from, DateTime to)
    {
        DateTime first = ToUtc(from).Date.AddDays(-Margin);
        DateTime last = ToUtc(to).Date.AddDays(Margin);
        int days = (last - first).Days + 1;

        return days is > 0 and <= MaxDays
            ? Enumerable.Range(0, days).Select(x => Value(first.AddDays(x))).ToList()
            : null;
    }

    /// <summary>
    /// <see cref="Value"/> for days after <paramref name="to"/>, past the margin, up to <see cref="FutureDays"/> after
    /// <paramref name="now"/>. Rows on these days are all after <paramref name="to"/>, so a range with no start can
    /// leave them out. Only the latest <see cref="MaxDays"/> when there are more, leaving out fewer is still right.
    /// Empty when <paramref name="to"/> is that late already.
    /// </summary>
    public static List<string> ValuesAfter(DateTime to, DateTime now)
    {
        DateTime first = ToUtc(to).Date.AddDays(Margin + 1);
        DateTime last = ToUtc(now).Date.AddDays(FutureDays);
        int days = Math.Min((last - first).Days + 1, MaxDays);

        return days > 0 ? Enumerable.Range(0, days).Select(x => Value(last.AddDays(-x))).ToList() : [];
    }

    // Stored dates are UTC, an unspecified one is taken as already UTC rather than shifted
    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
}
