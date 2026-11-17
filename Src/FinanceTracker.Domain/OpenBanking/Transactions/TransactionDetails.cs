namespace FinanceTracker.Domain.OpenBanking.Transactions;

/// <summary>
/// Provider view of a transaction, used to import or update a <see cref="BankTransaction"/>.
/// </summary>
/// <param name="ExternalId">The provider's id for it.</param>
/// <param name="Description">The bank's description, usually who it was with.</param>
/// <param name="Type">Whether money went out or came in.</param>
/// <param name="Category">The provider's category for it.</param>
/// <param name="Amount">How much it was, negative for money going out.</param>
/// <param name="Currency">Its currency code, e.g. GBP.</param>
/// <param name="OccurredAt">When it happened, in UTC.</param>
/// <param name="IsPending">Whether the bank hasn't settled it yet.</param>
/// <param name="ProviderClassifications">The tags the provider gives it.</param>
/// <param name="Metadata">The provider's raw details of who it was with.</param>
/// <param name="LabelledAsTransfer">Whether the provider labels it a transfer.</param>
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
