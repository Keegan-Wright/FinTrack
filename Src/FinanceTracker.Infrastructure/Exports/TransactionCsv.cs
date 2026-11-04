using System.Globalization;
using CsvHelper;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Infrastructure.Exports;

/// <summary>
/// Transactions as CSV, through CsvHelper RFC 4180 quoting, CRLF lines, a UTF-8 byte order mark so Excel reads
/// <c>£</c>, invariant numbers, and text a spreadsheet would run as a formula (starting <c>=</c>, <c>+</c>, <c>-</c>,
/// <c>@</c>, a tab or CR) escaped with a leading <c>'</c>, as descriptions come from the bank. Amounts stay numbers.
/// See <see cref="TransactionCsvRow"/> for the columns.
/// </summary>
public static class TransactionCsv
{
    private static readonly CsvWriterOptions Options = new()
    {
        NewLine = "\r\n",
        WriteByteOrderMark = true,
        Injection = CsvInjection.Escape,
        FormatProvider = CultureInfo.InvariantCulture
    };

    /// <summary>
    /// Writes the header then a line for each of <paramref name="transactions"/> as they arrive, so a large export
    /// never sits whole in memory.
    /// </summary>
    /// <param name="timeZone">Where the user is, for each transaction's day.</param>
    public static async Task WriteAsync(IAsyncEnumerable<Transaction> transactions, Stream destination,
        TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        await using CsvWriter writer = CsvWriter.Create(destination, Options, leaveOpen: true);
        await writer.WriteRecordsAsync(transactions.Select(x => TransactionCsvRow.From(x, timeZone)),
            cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }
}
