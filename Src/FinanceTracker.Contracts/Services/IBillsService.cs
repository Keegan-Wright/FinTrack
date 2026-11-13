using FinanceTracker.Contracts.Bills;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's bills, regular payments out of their accounts, how each period stands, and the subscriptions
/// suggested from their repeat payments.
/// </summary>
/// <remarks>
/// How a period stands is always worked out from the payments, in the server's time zone. Methods that look up by id
/// throw <see cref="InvalidOperationException"/> when the id doesn't exist or belongs to another user.
/// </remarks>
public interface IBillsService
{
    /// <summary>
    /// The user's bills running at any point in a month, with each one's periods due in it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The year or month is out of range.</exception>
    Task<BillMonthOverview> GetMonthAsync(int year, int month, CancellationToken cancellationToken);

    /// <summary>
    /// All of the user's bills, ended ones too, by name.
    /// </summary>
    IAsyncEnumerable<Bill> GetBillsAsync(CancellationToken cancellationToken);

    /// <exception cref="NotFoundException">The user has no bill with <paramref name="id"/>.</exception>
    Task<Bill> GetBillAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a bill the user keeps themselves, due from <see cref="SaveBill.NextDue"/>, and matches the payments
    /// already made to it. Validate the request with <see cref="Validation.SaveBillValidator"/> first.
    /// </summary>
    /// <returns>The bill as stored, with its new id.</returns>
    /// <exception cref="InvalidOperationException">The account isn't the user's.</exception>
    Task<Bill> AddBillAsync(SaveBill bill, CancellationToken cancellationToken);

    /// <summary>
    /// Changes one of the user's bills and matches its payments again. A new amount applies from today on, past
    /// periods keep theirs. A bill seeded from a standing order or direct debit stays on its account, and a standing
    /// order's schedule and amount follow the bank's at the next sync. Validate the request with
    /// <see cref="Validation.SaveBillValidator"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no bill with <paramref name="id"/>, or the account
    /// isn't theirs.</exception>
    Task<Bill> UpdateBillAsync(Guid id, SaveBill bill, CancellationToken cancellationToken);

    /// <summary>
    /// Ends one of the user's bills today, so nothing more is due. A sync won't reopen it.
    /// </summary>
    /// <exception cref="NotFoundException">The user has no bill with <paramref name="id"/>.</exception>
    Task<Bill> EndBillAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Makes an ended bill due again. A sync ends it again if its standing order or direct debit has gone.
    /// </summary>
    /// <exception cref="NotFoundException">The user has no bill with <paramref name="id"/>.</exception>
    Task<Bill> ReopenBillAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one of the user's bills, see <see cref="Bill.CanDelete"/>. Its payments pay nothing until matched
    /// again, and rules that paid it no longer do.
    /// </summary>
    /// <exception cref="NotFoundException">The user has no bill with <paramref name="id"/>.</exception>
    /// <exception cref="NotSupportedException">It was seeded from a standing order or direct debit, so it can only
    /// be ended.</exception>
    Task DeleteBillAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The user says which bill a transaction pays, or that it pays none. Matching and rules leave it alone after.
    /// </summary>
    /// <returns>The transaction as it is now.</returns>
    /// <exception cref="InvalidOperationException">The user has no transaction with
    /// <paramref name="transactionId"/>, or no bill with <see cref="PayBill.BillId"/>.</exception>
    Task<Transactions.Transaction> PayBillAsync(Guid transactionId, PayBill payment,
        CancellationToken cancellationToken);

    /// <summary>
    /// The subscriptions suggested from the user's repeat payments, waiting for them to confirm or dismiss, latest
    /// payment first.
    /// </summary>
    IAsyncEnumerable<BillSuggestion> GetSuggestionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// How many suggestions are waiting, for the navigation.
    /// </summary>
    Task<int> GetSuggestionCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Makes a suggestion a bill paid by the payments behind it, matched like any other from then on.
    /// </summary>
    /// <returns>The new bill.</returns>
    /// <exception cref="InvalidOperationException">The user has no waiting suggestion with <paramref name="id"/>.
    /// </exception>
    /// <exception cref="AlreadyABillException">A bill added since already covers its payments, the suggestion is
    /// removed.</exception>
    Task<Bill> ConfirmSuggestionAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The user says a suggestion isn't a bill, so it isn't suggested again.
    /// </summary>
    /// <exception cref="InvalidOperationException">The user has no waiting suggestion with <paramref name="id"/>.
    /// </exception>
    Task DismissSuggestionAsync(Guid id, CancellationToken cancellationToken);
}
