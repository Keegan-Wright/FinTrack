namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Provider view of a standing order, used to create or update a <see cref="StandingOrder"/>.
/// </summary>
/// <param name="Reference">The reference on its payments.</param>
/// <param name="Payee">Who it pays.</param>
/// <param name="Frequency">How often it pays, as the provider describes it.</param>
/// <param name="Status">The provider's status for it, e.g. active.</param>
/// <param name="Currency">The currency code of its payments.</param>
/// <param name="NextPaymentDate">When it next pays.</param>
/// <param name="NextPaymentAmount">What it next pays.</param>
/// <param name="FirstPaymentDate">When it first paid.</param>
/// <param name="FirstPaymentAmount">What it first paid.</param>
/// <param name="FinalPaymentDate">When it last pays.</param>
/// <param name="FinalPaymentAmount">What it last pays.</param>
/// <param name="Timestamp">When the provider reported it.</param>
public sealed record StandingOrderDetails(
    string Reference,
    string Payee,
    string Frequency,
    string Status,
    string Currency,
    DateTime NextPaymentDate,
    decimal NextPaymentAmount,
    DateTime FirstPaymentDate,
    decimal FirstPaymentAmount,
    DateTime FinalPaymentDate,
    decimal FinalPaymentAmount,
    DateTime Timestamp);
