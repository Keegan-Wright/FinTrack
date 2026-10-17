namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Provider view of a direct debit mandate, used to create or update a <see cref="DirectDebit"/>.
/// </summary>
public sealed record DirectDebitDetails(
    string ExternalId,
    string Name,
    string Status,
    DateTime PreviousPaymentDate,
    decimal PreviousPaymentAmount,
    string? Currency,
    DateTime Timestamp);
