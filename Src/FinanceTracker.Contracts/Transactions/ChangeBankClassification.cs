using System.ComponentModel;

namespace FinanceTracker.Contracts.Transactions;

/// <summary>
/// One of the bank's classifications on a transaction to hide or show again.
/// </summary>
public class ChangeBankClassification
{
    /// <summary>
    /// As the bank sent it, in any case.
    /// </summary>
    [Description("The bank's classification to hide or show")]
    public string Classification { get; set; } = string.Empty;
}
