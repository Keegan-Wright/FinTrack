namespace FinanceTracker.Ai.Tools.Debts;

/// <summary>
/// One of the user's debts.
/// </summary>
/// <param name="Name">Its name.</param>
/// <param name="Lender">Who it's owed to, if set.</param>
/// <param name="Owed">What's owed today.</param>
/// <param name="PaidOffPercent">How much of the starting balance is paid off, 0 to 100.</param>
/// <param name="InterestRate">The yearly interest rate, as a percentage.</param>
/// <param name="MonthlyPayment">What's paid towards it a month.</param>
/// <param name="PayOffGoal">When the user wants it paid off, if set.</param>
/// <param name="OnTrack">Whether it'll be paid off by the goal, null without one.</param>
public sealed record DebtSummary(string Name, string? Lender, decimal Owed, decimal PaidOffPercent,
    decimal InterestRate, decimal MonthlyPayment, DateOnly? PayOffGoal, bool? OnTrack);
