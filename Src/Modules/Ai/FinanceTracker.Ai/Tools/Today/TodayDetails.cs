namespace FinanceTracker.Ai.Tools.Today;

/// <summary>
/// Today and the periods it's in, for working out the dates of relative questions.
/// </summary>
/// <param name="Today">Today, in FinTrack's time zone.</param>
/// <param name="DayOfWeek">Today's day of the week.</param>
/// <param name="WeekStart">The first day of this week, by the install's culture.</param>
/// <param name="MonthStart">The first day of this month.</param>
/// <param name="YearStart">The first day of this year.</param>
/// <param name="Currency">The ISO 4217 code of the currency every amount is in, e.g. GBP.</param>
/// <param name="CurrencySymbol">That currency's symbol, e.g. £.</param>
public sealed record TodayDetails(DateOnly Today, DayOfWeek DayOfWeek, DateOnly WeekStart, DateOnly MonthStart,
    DateOnly YearStart, string? Currency, string CurrencySymbol);
