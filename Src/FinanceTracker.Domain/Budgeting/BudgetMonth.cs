namespace FinanceTracker.Domain.Budgeting;

/// <summary>
/// A calendar month, the period every <see cref="Budget"/> runs over.
/// </summary>
public readonly record struct BudgetMonth : IComparable<BudgetMonth>
{
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

    public int Year { get; }

    public int Month { get; }

    public DateOnly FirstDay => new(Year, Month, 1);

    public int Days => DateTime.DaysInMonth(Year, Month);

    public BudgetMonth Next => Month == 12 ? new BudgetMonth(Year + 1, 1) : new BudgetMonth(Year, Month + 1);

    public static BudgetMonth Of(DateOnly day) => new(day.Year, day.Month);

    /// <summary>
    /// The month <paramref name="utc"/> falls in where the user is, so a payment just after midnight on the 1st
    /// counts in the new month even when it's still the old one in UTC.
    /// </summary>
    public static BudgetMonth Of(DateTime utc, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        DateTime local = TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
        return new BudgetMonth(local.Year, local.Month);
    }

    public int CompareTo(BudgetMonth other) => (Year, Month).CompareTo((other.Year, other.Month));

    public static bool operator <(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) < 0;

    public static bool operator >(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) > 0;

    public static bool operator <=(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) <= 0;

    public static bool operator >=(BudgetMonth left, BudgetMonth right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
