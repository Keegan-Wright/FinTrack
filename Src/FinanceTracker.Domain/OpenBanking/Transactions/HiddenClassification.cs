namespace FinanceTracker.Domain.OpenBanking.Transactions;

/// <summary>
/// One of the bank's <see cref="BankTransaction.ProviderClassifications"/> that's hidden from the user's tags, filters
/// and reports.
/// </summary>
/// <param name="Classification">As the bank sent it.</param>
/// <param name="RuleId">The rule that hid it, null when the user did.</param>
public sealed record HiddenClassification(string Classification, Guid? RuleId)
{
    /// <summary>
    /// Whether the user hid it, which no rule changes.
    /// </summary>
    public bool HiddenByUser => RuleId is null;
}
