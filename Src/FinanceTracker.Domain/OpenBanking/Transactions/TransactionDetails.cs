namespace FinanceTracker.Domain.OpenBanking.Transactions;

/// <summary>
/// Provider view of a transaction, used to import or update a <see cref="BankTransaction"/>.
/// </summary>
public sealed record TransactionDetails(
    string ExternalId,
    string Description,
    TransactionType Type,
    string Category,
    decimal Amount,
    string Currency,
    DateTime OccurredAt,
    bool IsPending,
    IReadOnlyCollection<string> ProviderClassifications,
    TransactionMetadata Metadata,
    bool LabelledAsTransfer);
