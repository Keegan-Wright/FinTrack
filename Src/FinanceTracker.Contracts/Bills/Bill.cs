using System.ComponentModel;

namespace FinanceTracker.Contracts.Bills;

/// <summary>
/// One of the user's bills, a regular payment out of an account.
/// </summary>
public class Bill
{
    /// <summary>
    /// Unique identifier for the bill.
    /// </summary>
    [Description("Unique identifier for the bill")]
    public Guid Id { get; init; }

    /// <summary>
    /// The bill's name.
    /// </summary>
    [Description("The bill's name")]
    public required string Name { get; init; }

    /// <summary>
    /// Who it's paid to as the bank names them, what payments are matched on.
    /// </summary>
    [Description("Who the bill is paid to, as the bank names them")]
    public required string Payee { get; init; }

    /// <summary>
    /// The account the bill is paid from.
    /// </summary>
    [Description("The account the bill is paid from")]
    public Guid AccountId { get; init; }

    /// <summary>
    /// The name of the account the bill is paid from.
    /// </summary>
    [Description("The name of the account the bill is paid from")]
    public required string AccountName { get; init; }

    /// <summary>
    /// Where the bill came from.
    /// </summary>
    [Description("Where the bill came from")]
    public BillSource Source { get; init; }

    /// <summary>
    /// How often the bill is due.
    /// </summary>
    [Description("How often the bill is due")]
    public BillFrequency Frequency { get; init; }

    /// <summary>
    /// The first due date, nothing is due before.
    /// </summary>
    [Description("The bill's first due date")]
    public DateOnly StartsOn { get; init; }

    /// <summary>
    /// The next due date from today, null once it has ended.
    /// </summary>
    [Description("The bill's next due date, null once it has ended")]
    public DateOnly? NextDue { get; init; }

    /// <summary>
    /// The last day it could be due, null while it runs.
    /// </summary>
    [Description("The last day the bill could be due, null while it runs")]
    public DateOnly? EndedOn { get; init; }

    /// <summary>
    /// Whether the user ended it rather than the bank, which only the user can undo.
    /// </summary>
    [Description("Whether the user ended the bill")]
    public bool EndedByUser { get; init; }

    /// <summary>
    /// What it's expected to cost now.
    /// </summary>
    [Description("What the bill is expected to cost now")]
    public decimal ExpectedAmount { get; init; }

    /// <summary>
    /// How far a payment can be from the expected amount, as a percentage of it, and still count as paid.
    /// </summary>
    [Description("How far a payment can be from the expected amount, as a percentage, and still count as paid")]
    public decimal TolerancePercent { get; init; }

    /// <summary>
    /// What it costs a month at its expected amount, whatever its frequency, 0 once ended.
    /// </summary>
    [Description("What the bill costs a month, whatever its frequency")]
    public decimal MonthlyCost { get; init; }

    /// <summary>
    /// Its latest payment's amount, null before it's been paid.
    /// </summary>
    [Description("The bill's latest payment, null before it's been paid")]
    public decimal? LastPaid { get; init; }

    /// <summary>
    /// The day of the bill's latest payment, null before it's been paid.
    /// </summary>
    [Description("The day of the bill's latest payment, null before it's been paid")]
    public DateOnly? LastPaidOn { get; init; }

    /// <summary>
    /// Whether its schedule and amount follow the bank's, so editing them won't stick. True for standing orders.
    /// </summary>
    [Description("Whether the bill's schedule and amount follow the bank's")]
    public bool FollowsTheBank { get; init; }

    /// <summary>
    /// Whether it can be deleted. Bills seeded from a standing order or direct debit can only be ended, the next sync
    /// would seed them again.
    /// </summary>
    [Description("Whether the bill can be deleted rather than only ended")]
    public bool CanDelete { get; init; }

    /// <summary>
    /// The first and last name of whoever shares it with the user, null when it's their own. Shared because it's paid
    /// from an account they share, or because they chose to share it.
    /// </summary>
    [Description("Who shares the bill with the user, empty when it's their own")]
    public string? SharedBy { get; init; }

    /// <summary>
    /// Whether the user can edit, end and reopen it: their own, or shared with them with changes allowed. Only its owner
    /// deletes it.
    /// </summary>
    [Description("Whether the user can edit the bill")]
    public bool CanChange { get; init; } = true;
}
