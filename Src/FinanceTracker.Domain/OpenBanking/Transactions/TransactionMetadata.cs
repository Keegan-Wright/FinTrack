namespace FinanceTracker.Domain.OpenBanking.Transactions;

/// <summary>
/// What the provider says about who a transaction was with. Kept raw as each bank fills in different fields, e.g.
/// Lloyds puts the other account's sort code and number in <see cref="ProviderReference"/> while Monzo only sends
/// <see cref="ProviderCategory"/>.
/// </summary>
/// <param name="ProviderReference">The bank's reference, a transfer's other account or a direct debit's mandate.</param>
/// <param name="ProviderCategory">The bank's own category code, e.g. <c>TFR</c>, <c>DD</c> or <c>uk_retail_pot</c>.</param>
/// <param name="MerchantName">The merchant on a card payment.</param>
/// <param name="CounterPartyPreferredName">The other party's name, when the bank sends one.</param>
/// <param name="DebtorAccountName">The paying account's name, when the bank sends one.</param>
public sealed record TransactionMetadata(
    string? ProviderReference,
    string? ProviderCategory,
    string? MerchantName,
    string? CounterPartyPreferredName,
    string? DebtorAccountName)
{
    public static TransactionMetadata None { get; } = new(null, null, null, null, null);
}
