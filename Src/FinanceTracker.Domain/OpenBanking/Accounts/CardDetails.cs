namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// What the provider says about a card <see cref="BankAccount"/>.
/// </summary>
/// <param name="Network">The card network, e.g. <c>VISA</c>.</param>
/// <param name="CardType">The kind of card, e.g. <c>CREDIT</c> or <c>CHARGE</c>.</param>
/// <param name="PartialNumber">The last digits of the card number.</param>
/// <param name="NameOnCard">The cardholder's name as printed.</param>
/// <param name="ValidTo">When the card expires, if the provider says.</param>
public sealed record CardDetails(
    string? Network,
    string? CardType,
    string? PartialNumber,
    string? NameOnCard,
    DateTime? ValidTo);
