using FinanceTracker.Contracts.Debts;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's debts, what each owes and when it's forecast to be paid off.
/// </summary>
/// <remarks>
/// What's owed is always worked out, from the payments linked to a debt with interest added monthly, or from its
/// linked account's balance. Methods that look up by id throw <see cref="InvalidOperationException"/> when the id
/// doesn't exist or belongs to another user.
/// </remarks>
public interface IDebtsService
{
    /// <summary>
    /// All of the user's debts, by name.
    /// </summary>
    IAsyncEnumerable<Debt> GetDebtsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One of the user's debts with its payments and what it owed over time.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no debt with <paramref name="id"/>.</exception>
    Task<DebtDetail> GetDebtAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a debt. Validate the request with <see cref="Validation.SaveDebtValidator"/> first.
    /// </summary>
    /// <returns>The debt as stored, with its new id.</returns>
    /// <exception cref="InvalidOperationException">The account isn't the user's.</exception>
    /// <exception cref="NameTakenException">The user already has a debt called that, paid off or not, in any case.
    /// </exception>
    Task<Debt> AddDebtAsync(SaveDebt debt, CancellationToken cancellationToken);

    /// <summary>
    /// Changes one of the user's debts. Validate the request with <see cref="Validation.SaveDebtValidator"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no debt with <paramref name="id"/>, or the account
    /// isn't theirs.</exception>
    /// <exception cref="NameTakenException">The user already has another debt called that, paid off or not, in any
    /// case.</exception>
    Task<Debt> UpdateDebtAsync(Guid id, SaveDebt debt, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's debts. Its payments pay off nothing, and rules that paid it off no longer do.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no debt with <paramref name="id"/>.</exception>
    Task DeleteDebtAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// When one of the user's debts would be paid off paying <paramref name="monthlyPayment"/> a month from today
    /// instead, to compare with its forecast.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no debt with <paramref name="id"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="monthlyPayment"/> isn't more than 0.</exception>
    Task<DebtForecast> ForecastAsync(Guid id, decimal monthlyPayment, CancellationToken cancellationToken);

    /// <summary>
    /// The user says which debt a transaction pays off, or that it pays none. Rules leave it alone after.
    /// </summary>
    /// <returns>The transaction as it is now.</returns>
    /// <exception cref="InvalidOperationException">The user has no transaction with
    /// <paramref name="transactionId"/>, or no debt with <see cref="PayDebt.DebtId"/>.</exception>
    Task<Transactions.Transaction> PayDebtAsync(Guid transactionId, PayDebt payment,
        CancellationToken cancellationToken);
}
