namespace FinanceTracker.Domain.Debts;

/// <summary>
/// How a <see cref="Debt"/> is doing, from <see cref="DebtCalculator"/>.
/// </summary>
/// <param name="Owed">What's owed today.</param>
/// <param name="Paid">The payments linked to it since it started.</param>
/// <param name="PaidOffPercent">How much of the starting balance is paid off, from 0 to 100.</param>
/// <param name="Forecast">When it's paid off at the monthly payment.</param>
/// <param name="OnTrack">Whether the forecast is on or before the pay-off goal, null without a goal.</param>
public sealed record DebtProgress(decimal Owed, decimal Paid, decimal PaidOffPercent, DebtForecast Forecast,
    bool? OnTrack);
