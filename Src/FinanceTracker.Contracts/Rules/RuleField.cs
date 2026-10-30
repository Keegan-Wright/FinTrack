using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Rules;

/// <summary>
/// What part of a transaction a <see cref="RuleCondition"/> looks at.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RuleField>))]
public enum RuleField
{
    /// <summary>
    /// The description, text.
    /// </summary>
    Description = 1,

    /// <summary>
    /// The amount ignoring its sign, so 3.50 matches a payment of 3.50 in or out. Use <see cref="Type"/> for the
    /// direction.
    /// </summary>
    Amount = 2,

    /// <summary>
    /// Debit or credit.
    /// </summary>
    Type = 3,

    /// <summary>
    /// The account it's on, by id.
    /// </summary>
    Account = 4,

    /// <summary>
    /// The provider's category e.g. <c>PURCHASE</c> or <c>DIRECT_DEBIT</c>, text.
    /// </summary>
    Category = 5,

    /// <summary>
    /// The bank's own code e.g. <c>DD</c> or <c>TFR</c>, text.
    /// </summary>
    ProviderCategory = 6,

    /// <summary>
    /// Any of the provider's classifications, text.
    /// </summary>
    ProviderClassification = 7,

    /// <summary>
    /// The merchant or counterparty name, whichever the bank sent, text.
    /// </summary>
    Counterparty = 8
}
