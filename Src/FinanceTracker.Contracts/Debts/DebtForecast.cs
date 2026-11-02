using System.ComponentModel;

namespace FinanceTracker.Contracts.Debts;

/// <summary>
/// When a debt is paid off from what's owed now, paying the same each month.
/// </summary>
public class DebtForecast
{
    /// <summary>
    /// The month it's paid off, null when the payment doesn't cover the interest so it's never paid off.
    /// </summary>
    [Description("When the debt is paid off, null when it never is")]
    public DateOnly? PaidOffOn { get; init; }

    /// <summary>
    /// How many more payments it takes, null when it's never paid off.
    /// </summary>
    [Description("How many more monthly payments it takes")]
    public int? Months { get; init; }

    /// <summary>
    /// The interest still to pay on the way, null when it's never paid off.
    /// </summary>
    [Description("The interest still to pay")]
    public decimal? Interest { get; init; }
}
