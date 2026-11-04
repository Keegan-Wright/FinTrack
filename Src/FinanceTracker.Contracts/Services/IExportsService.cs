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

    /// <summary>
    /// Writes a backup of everything the user has to <paramref name="destination"/> as a zip. A JSON file for each
    /// kind of data, in the shapes the API returns, a <c>transactions.csv</c> as
    /// <see cref="WriteTransactionsCsvAsync"/> writes it, and a <c>manifest.json</c> saying when it was made, which
    /// format it's in and how many of each there are. Not encrypted.
    /// </summary>
    Task WriteBackupAsync(Stream destination, CancellationToken cancellationToken);
}
