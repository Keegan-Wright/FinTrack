namespace FinanceTracker.Contracts.Calendar;

/// <summary>
/// Which of a debt's dates a <see cref="CalendarDebtItem"/> is.
/// </summary>
public enum CalendarDebtDate
{
    /// <summary>
    /// When it's forecast to be paid off at its monthly payment.
    /// </summary>
    PaidOff = 0,

    /// <summary>
    /// When the user wants it paid off by.
    /// </summary>
    Goal = 1
}
