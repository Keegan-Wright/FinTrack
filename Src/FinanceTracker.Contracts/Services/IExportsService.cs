using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's data as files to keep, open in a spreadsheet or take elsewhere. Only ever their own.
/// </summary>
public interface IExportsService
{
    /// <summary>
    /// Writes the user's transactions matching <paramref name="filter"/> to <paramref name="destination"/> as CSV, the
    /// same ones the transactions page lists for it, newest first. UTF-8 with a byte order mark, so spreadsheets read
    /// <c>£</c> and accents, dates <c>dd/MM/yyyy</c> where the user is, and amounts with a dot and no currency symbol,
    /// so they read as numbers.
    /// </summary>
    Task WriteTransactionsCsvAsync(FilterTransactions filter, Stream destination, CancellationToken cancellationToken);
}
