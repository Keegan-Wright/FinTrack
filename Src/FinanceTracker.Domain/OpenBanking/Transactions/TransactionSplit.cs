namespace FinanceTracker.Domain.OpenBanking.Transactions;

/// <summary>
/// One part of a split <see cref="BankTransaction"/>, e.g. the household half of a supermarket shop. A transaction's
/// splits add up to its amount.
/// </summary>
/// <param name="Amount">Signed like the transaction, never zero.</param>
/// <param name="TagId">The tag this part counts under, null to leave it untagged.</param>
/// <param name="Note">What this part was for, when the user says.</param>
public sealed record TransactionSplit(decimal Amount, Guid? TagId, string? Note);
