namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Provider view of a standing order, used to create or update a <see cref="StandingOrder"/>.
/// </summary>
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
