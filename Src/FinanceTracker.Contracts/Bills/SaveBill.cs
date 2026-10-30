using System.ComponentModel;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// A new bill, or everything the user can change about an existing one.
/// </summary>
public class SaveBill
{
    /// <summary>
    /// The bill's name.
    /// </summary>
    [Description("The bill's name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Who it's paid to as the bank names them, e.g. as it reads on a statement.
    /// </summary>
    [Description("Who the bill is paid to, as it reads on a statement")]
    public string Payee { get; set; } = string.Empty;

    /// <summary>
    /// The account it's paid from. Left as it is for a bill seeded from a standing order or direct debit.
    /// </summary>
    [Description("The account the bill is paid from")]
    public Guid AccountId { get; set; }

    /// <summary>
    /// How often the bill is due.
    /// </summary>
    [Description("How often the bill is due")]
    public BillFrequency Frequency { get; set; } = BillFrequency.Monthly;

    /// <summary>
    /// A due date the others are counted from, the first for a new bill.
    /// </summary>
    [Description("A due date, the first for a new bill")]
    public DateOnly NextDue { get; set; }

    /// <summary>
    /// What it's expected to cost, from today on for an existing bill.
    /// </summary>
    [Description("What the bill is expected to cost each time")]
    public decimal ExpectedAmount { get; set; }

    /// <summary>
    /// How far a payment can be from the expected amount, as a percentage, and still count as paid.
    /// </summary>
    [Description("How far a payment can be from the expected amount, as a percentage, and still count as paid")]
    public decimal TolerancePercent { get; set; } = 5;
}
