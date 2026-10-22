using System.ComponentModel;

namespace FinanceTracker.Contracts.Calendar;

/// <summary>
/// A transaction shown on its calendar day.
/// </summary>
public class CalendarTransactionItem
{
    /// <summary>
    /// Signed in the account's currency: negative is money out, positive is money in.
    /// </summary>
    [Description("Amount of the scheduled transaction")]
    public required decimal Amount { get; init; }

    /// <summary>
    /// The bank's description, usually the payee or merchant.
    /// </summary>
    [Description("Description of the scheduled transaction")]
    public required string Description { get; init; }

    /// <summary>
    /// <c>Debit</c> or <c>Credit</c>.
    /// </summary>
    [Description("Type of the scheduled transaction")]
    public required string TransactionType { get; init; }

    /// <summary>
    /// When the transaction happened.
    /// </summary>
    [Description("Scheduled date and time of the transaction")]
    public required DateTime TransactionTime { get; init; }
}
