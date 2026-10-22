using System.ComponentModel;

namespace FinanceTracker.Contracts.Account;

/// <summary>
/// A transaction as listed under its account.
/// </summary>
public class AccountTransaction
{
    /// <summary>
    /// The bank's description, usually the payee or merchant.
    /// </summary>
    [Description("Description of the transaction")]
    public required string Description { get; init; }

    /// <summary>
    /// Signed in the account's currency, negative is money out, positive is money in.
    /// </summary>
    [Description("Amount of the transaction")]
    public required decimal Amount { get; init; }

    /// <summary>
    /// When the transaction happened in UTC.
    /// </summary>
    [Description("Time when the transaction occurred")]
    public required DateTime Time { get; init; }

    /// <summary>
    /// <c>Pending</c> until the bank settles it, then <c>Complete</c>.
    /// </summary>
    [Description("Status of the transaction")]
    public required string Status { get; init; }
}
