using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's transactions and the values they can be filtered by.
/// </summary>
/// <remarks>
/// Most transaction columns are encrypted, so filtering beyond account and provider happens after decryption.
/// </remarks>
public interface ITransactionsService
{
    /// <summary>
    /// The user's transactions matching <paramref name="filterTransactions"/>, newest first.
    /// </summary>
    /// <param name="filterTransactions">The filters, empty ones match everything.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    IAsyncEnumerable<Transaction> GetAllTransactionsAsync(FilterTransactions filterTransactions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hides one of the bank's classifications on one of the user's transactions, leaving it out of its tags, the
    /// filters and reports. No rule shows it again. Validate the request with
    /// <see cref="Validation.ChangeBankClassificationValidator"/> first.
    /// </summary>
    /// <returns>The transaction as it is now.</returns>
    /// <exception cref="InvalidOperationException">The user has no transaction with <paramref name="transactionId"/>,
    /// or the bank didn't classify it that way.</exception>
    Task<Transaction> HideBankClassificationAsync(Guid transactionId, ChangeBankClassification request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Shows again a bank classification the user hid on one of their transactions. One a rule hid stays hidden until
    /// the rule changes. Validate the request with <see cref="Validation.ChangeBankClassificationValidator"/> first.
    /// </summary>
    /// <returns>The transaction as it is now.</returns>
    /// <exception cref="InvalidOperationException">The user has no transaction with
    /// <paramref name="transactionId"/>.</exception>
    Task<Transaction> ShowBankClassificationAsync(Guid transactionId, ChangeBankClassification request,
        CancellationToken cancellationToken);

    /// <summary>
    /// The user's accounts, for the account filter.
    /// </summary>
    IAsyncEnumerable<TransactionAccountFilter> GetAccountsForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// The user's bank connections, for the provider filter.
    /// </summary>
    IAsyncEnumerable<TransactionProviderFilter> GetProvidersForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Each transaction type found in the user's transactions, once.
    /// </summary>
    IAsyncEnumerable<TransactionTypeFilter> GetTypesForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Each provider category found in the user's transactions, once.
    /// </summary>
    IAsyncEnumerable<TransactionCategoryFilter> GetCategoriesForTransactionFiltersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Each tag the user can filter by, once: provider classifications found on their transactions and all their
    /// custom classifications.
    /// </summary>
    IAsyncEnumerable<TransactionTagFilter> GetTagsForTransactionFiltersAsync(
        CancellationToken cancellationToken);
}
