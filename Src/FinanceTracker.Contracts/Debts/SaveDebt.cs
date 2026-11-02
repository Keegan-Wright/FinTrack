using System.ComponentModel;

namespace FinanceTracker.Contracts.Debts;

/// <summary>
/// A debt to add or the changes to one, see <c>SaveDebtValidator</c>.
/// </summary>
public class SaveDebt
{
    /// <summary>
    /// The debt's name.
    /// </summary>
    [Description("The debt's name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Who it's owed to, blank for none.
    /// </summary>
    [Description("Who the debt is owed to")]
    public string? Lender { get; set; }

    /// <summary>
    /// What was owed on <see cref="StartedOn"/>.
    /// </summary>
    [Description("What was owed when the debt started")]
    public decimal StartingBalance { get; set; }

    /// <summary>
    /// The day <see cref="StartingBalance"/> was owed.
    /// </summary>
    [Description("The day the starting balance was owed")]
    public DateOnly StartedOn { get; set; }

    /// <summary>
    /// The yearly rate (APR) as a percentage, 0 for none.
    /// </summary>
    [Description("The yearly interest rate (APR) as a percentage")]
    public decimal InterestRate { get; set; }

    /// <summary>
    /// What the user pays each month.
    /// </summary>
    [Description("What the user pays each month")]
    public decimal MonthlyPayment { get; set; }

    /// <summary>
    /// The loan or card account whose balance says what's owed, null to work it out from the payments.
    /// </summary>
    [Description("The account whose balance says what's owed, null to work it out from the payments")]
    public Guid? AccountId { get; set; }

    /// <summary>
    /// When the user wants it paid off by, null for no goal.
    /// </summary>
    [Description("When the user wants the debt paid off by")]
    public DateOnly? PayOffGoal { get; set; }
}
