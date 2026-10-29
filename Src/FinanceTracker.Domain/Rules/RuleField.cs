using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Rules;

/// <summary>
/// What part of a <see cref="BankTransaction"/> a <see cref="RuleCondition"/> looks at.
/// </summary>
public enum RuleField
{
    /// <summary>
    /// <see cref="BankTransaction.Description"/>, text.
    /// </summary>
    Description = 1,

    /// <summary>
    /// <see cref="BankTransaction.Amount"/> ignoring its sign, so 3.50 matches a payment of 3.50 in or out. Use
    /// <see cref="Type"/> for the direction.
    /// </summary>
    Amount = 2,

    /// <summary>
    /// <see cref="BankTransaction.Type"/>, debit or credit.
    /// </summary>
    Type = 3,

    /// <summary>
    /// <see cref="BankTransaction.AccountId"/>, the account it's on.
    /// </summary>
    Account = 4,

    /// <summary>
    /// <see cref="BankTransaction.Category"/>, the provider's category e.g. <c>PURCHASE</c> or <c>DIRECT_DEBIT</c>,
    /// text.
    /// </summary>
    Category = 5,

    /// <summary>
    /// <see cref="TransactionMetadata.ProviderCategory"/>, the bank's own code e.g. <c>DD</c> or <c>TFR</c>, text.
    /// </summary>
    ProviderCategory = 6,

    /// <summary>
    /// Any of <see cref="BankTransaction.ProviderClassifications"/>, text.
    /// </summary>
    ProviderClassification = 7,

    /// <summary>
    /// <see cref="TransactionMetadata.MerchantName"/> or <see cref="TransactionMetadata.CounterPartyPreferredName"/>,
    /// whichever the bank sent, text.
    /// </summary>
    Counterparty = 8
}
