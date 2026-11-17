namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Provider view of a direct debit mandate, used to create or update a <see cref="DirectDebit"/>.
/// </summary>
/// <param name="ExternalId">The provider's id for it.</param>
/// <param name="Name">Who it pays.</param>
/// <param name="Status">The provider's status for it, e.g. active.</param>
/// <param name="PreviousPaymentDate">When it last paid.</param>
/// <param name="PreviousPaymentAmount">What it last paid.</param>
/// <param name="Currency">The currency code of its payments, null when the provider doesn't say.</param>
/// <param name="Timestamp">When the provider reported it.</param>
public sealed record DirectDebitDetails(
    string ExternalId,
    string Name,
    string Status,
    DateTime PreviousPaymentDate,
    decimal PreviousPaymentAmount,
    string? Currency,
    DateTime Timestamp);
