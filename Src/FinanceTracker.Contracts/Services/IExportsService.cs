using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.PayPeriods;
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
    /// <c>£</c> and accents, the day where the user is as the app's culture writes a date, and amounts with a dot and
    /// no currency symbol, so they read as numbers.
    /// </summary>
    Task WriteTransactionsCsvAsync(FilterTransactions filter, Stream destination, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a backup of everything the user has to <paramref name="destination"/> as a zip. A JSON file for each
    /// kind of data, in the shapes the API returns, a <c>transactions.csv</c> as
    /// <see cref="WriteTransactionsCsvAsync"/> writes it, and a <c>manifest.json</c> saying when it was made, which
    /// format it's in and how many of each there are. Not encrypted.
    /// </summary>
    Task WriteBackupAsync(Stream destination, CancellationToken cancellationToken);

    /// <summary>
    /// The pay periods there's a statement for, newest first: today's back to the one the user's oldest transaction
    /// is in. Just today's when they have no transactions yet.
    /// </summary>
    Task<List<PayPeriod>> GetStatementPeriodsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes a PDF statement of the pay period named after <paramref name="month"/> to
    /// <paramref name="destination"/>: money in and out across all accounts, spending by tag, bills, budgets, savings
    /// goals and debts as they stand now, and every transaction in the period. A calendar month when the user's pay
    /// isn't picked up.
    /// </summary>
    Task WriteStatementAsync(int year, int month, Stream destination, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a PDF of the report <paramref name="report"/> asks for to <paramref name="destination"/>: the
    /// breakdown the reports page shows for its filters, with each year's months as a chart and a table.
    /// </summary>
    Task WriteReportAsync(ExportReport report, Stream destination, CancellationToken cancellationToken);
}
