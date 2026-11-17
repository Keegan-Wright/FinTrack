namespace FinanceTracker.Domain.Budgeting;

/// <summary>
/// A calendar month, the period every <see cref="Budget"/> runs over.
/// </summary>
public readonly record struct BudgetMonth : IComparable<BudgetMonth>
{
    /// <param name="year">The year, 1 to 9998.</param>
    /// <param name="month">The month of the year, 1 to 12.</param>
    /// <exception cref="ArgumentOutOfRangeException">The year or month is out of range.</exception>
    public BudgetMonth(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9998);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);

        Year = year;
        Month = month;
    }

    /// <summary>
    /// The year, e.g. 2026.
    /// </summary>
    public int Year { get; }

    /// <summary>
    /// The month of the year, 1 to 12.
    /// </summary>
    public int Month { get; }

    /// <summary>
    /// Its first day.
    /// </summary>
    public DateOnly FirstDay => new(Year, Month, 1);

    /// <summary>
    /// How many days it has.
    /// </summary>
    public int Days => DateTime.DaysInMonth(Year, Month);

    /// <summary>
    /// The month after.
    /// </summary>
    public BudgetMonth Next => Month == 12 ? new BudgetMonth(Year + 1, 1) : new BudgetMonth(Year, Month + 1);

    /// <summary>
    /// The month <paramref name="day"/> falls in.
    /// </summary>
    /// <param name="day">Any day in the month.</param>
    public static BudgetMonth Of(DateOnly day) => new(day.Year, day.Month);

    /// <summary>
    /// The month <paramref name="utc"/> falls in where the user is, so a payment just after midnight on the 1st
    /// counts in the new month even when it's still the old one in UTC.
    /// </summary>
    /// <param name="utc">The time in UTC.</param>
    /// <param name="timeZone">Where the user is.</param>
    public static BudgetMonth Of(DateTime utc, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        DateTime local = TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
        return new BudgetMonth(local.Year, local.Month);
    }

    /// <inheritdoc/>
    public int CompareTo(BudgetMonth other) => (Year, Month).CompareTo((other.Year, other.Month));

    /// <summary>
    /// Whether <paramref name="left"/> comes before <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first month.</param>
    /// <param name="right">The second month.</param>
    public static bool operator <(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Whether <paramref name="left"/> comes after <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first month.</param>
    /// <param name="right">The second month.</param>
    public static bool operator >(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Whether <paramref name="left"/> is the same month as <paramref name="right"/> or before it.
    /// </summary>
    /// <param name="left">The first month.</param>
    /// <param name="right">The second month.</param>
    public static bool operator <=(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Whether <paramref name="left"/> is the same month as <paramref name="right"/> or after it.
    /// </summary>
    /// <param name="left">The first month.</param>
    /// <param name="right">The second month.</param>
    public static bool operator >=(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// The month as year and month, e.g. 2026-11.
    /// </summary>
    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
