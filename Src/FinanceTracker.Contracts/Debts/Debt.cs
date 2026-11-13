using System.ComponentModel;

namespace FinanceTracker.Contracts.Debts;

/// <summary>
/// Money the user owes and is paying off, with what's owed now and when it's forecast to be paid off.
/// </summary>
public class Debt
{
    /// <summary>
    /// Unique identifier for the debt.
    /// </summary>
    [Description("Unique identifier for the debt")]
    public Guid Id { get; init; }

    /// <summary>
    /// The debt's name.
    /// </summary>
    [Description("The debt's name")]
    public required string Name { get; init; }

    /// <summary>
    /// Who it's owed to, null when the user didn't say.
    /// </summary>
    [Description("Who the debt is owed to")]
    public string? Lender { get; init; }

    /// <summary>
    /// What was owed on <see cref="StartedOn"/>.
    /// </summary>
    [Description("What was owed when the debt started")]
    public decimal StartingBalance { get; init; }

    /// <summary>
    /// The day <see cref="StartingBalance"/> was owed, payments and interest count from then.
    /// </summary>
    [Description("The day the starting balance was owed")]
    public DateOnly StartedOn { get; init; }

    /// <summary>
    /// The yearly rate (APR) as a percentage, 0 for none.
    /// </summary>
    [Description("The yearly interest rate (APR) as a percentage")]
    public decimal InterestRate { get; init; }

    /// <summary>
    /// What the user pays each month.
    /// </summary>
    [Description("What the user pays each month")]
    public decimal MonthlyPayment { get; init; }

    /// <summary>
    /// The loan or card account whose balance says what's owed, null when worked out from the payments.
    /// </summary>
    [Description("The account whose balance says what's owed, when linked")]
    public Guid? AccountId { get; init; }

    /// <summary>
    /// The name of <see cref="AccountId"/>, null when not linked.
    /// </summary>
    [Description("The name of the linked account")]
    public string? AccountName { get; init; }

    /// <summary>
    /// When the user wants it paid off by, null without a goal.
    /// </summary>
    [Description("When the user wants the debt paid off by")]
    public DateOnly? PayOffGoal { get; init; }

    /// <summary>
    /// What's owed today.
    /// </summary>
    [Description("What's owed today")]
    public decimal Owed { get; init; }

    /// <summary>
    /// The payments linked to it since it started.
    /// </summary>
    [Description("The payments linked to the debt since it started")]
    public decimal Paid { get; init; }

    /// <summary>
    /// How much of the starting balance is paid off, from 0 to 100.
    /// </summary>
    [Description("How much of the starting balance is paid off, as a percentage")]
    public decimal PaidOffPercent { get; init; }

    /// <summary>
    /// When it's paid off at <see cref="MonthlyPayment"/>.
    /// </summary>
    [Description("When the debt is paid off at the monthly payment")]
    public required DebtForecast Forecast { get; init; }

    /// <summary>
    /// Whether <see cref="Forecast"/> is on or before <see cref="PayOffGoal"/>, null without a goal.
    /// </summary>
    [Description("Whether the forecast meets the pay-off goal, null without a goal")]
    public bool? OnTrack { get; init; }

    /// <summary>
    /// The first and last name of whoever shares it with the user, null when it's their own. Shared because it follows
    /// an account they share, or because they chose to share it.
    /// </summary>
    [Description("Who shares the debt with the user, empty when it's their own")]
    public string? SharedBy { get; init; }

    /// <summary>
    /// Whether the user can edit it: their own, or shared with them with changes allowed. Only its owner deletes it or
    /// changes which account it follows.
    /// </summary>
    [Description("Whether the user can edit the debt")]
    public bool CanChange { get; init; } = true;
}
