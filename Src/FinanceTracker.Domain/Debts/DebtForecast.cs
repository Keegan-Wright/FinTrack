namespace FinanceTracker.Domain.Debts;

/// <summary>
/// When a debt is paid off from what's owed now, paying the same each month, from
/// <see cref="DebtCalculator.Forecast"/>.
/// </summary>
/// <param name="PaidOffOn">The month it's paid off, today when nothing is owed, null when the payment never clears it
/// because it doesn't cover the interest.</param>
/// <param name="Months">How many more payments it takes, null when it's never paid off.</param>
/// <param name="Interest">The interest still to pay on the way, null when it's never paid off.</param>
public sealed record DebtForecast(DateOnly? PaidOffOn, int? Months, decimal? Interest)
{
    public static DebtForecast Never { get; } = new(null, null, null);

    public bool IsNeverPaidOff => PaidOffOn is null;
}
