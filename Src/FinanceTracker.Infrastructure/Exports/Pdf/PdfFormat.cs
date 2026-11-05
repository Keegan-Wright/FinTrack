using System.Globalization;

namespace FinanceTracker.Infrastructure.Exports.Pdf;

/// <summary>
/// How the PDFs write money and dates, in the culture the server runs in, as the app's pages are.
/// </summary>
internal static class PdfFormat
{
    /// <summary>
    /// The environment's culture, read each time so it follows the one the server is given.
    /// </summary>
    public static CultureInfo Culture => CultureInfo.CurrentCulture;

    /// <summary>
    /// e.g. £1,234.56 or -£3.50 in en-GB.
    /// </summary>
    public static string Money(decimal amount) => amount.ToString("C", Culture);

    /// <summary>
    /// e.g. 4 Nov.
    /// </summary>
    public static string ShortDate(DateOnly day) => day.ToString("d MMM", Culture);

    /// <summary>
    /// e.g. 4 November 2026.
    /// </summary>
    public static string LongDate(DateOnly day) => day.ToString("d MMMM yyyy", Culture);

    /// <summary>
    /// e.g. 30 Oct to 27 Nov 2026, or 30 Dec 2025 to 27 Jan 2026 across a new year.
    /// </summary>
    public static string DateRange(DateOnly start, DateOnly end)
    {
        string format = start.Year == end.Year ? "d MMM" : "d MMM yyyy";
        return $"{start.ToString(format, Culture)} to {end.ToString("d MMM yyyy", Culture)}";
    }

    /// <summary>
    /// e.g. 1 transaction, 12 transactions.
    /// </summary>
    public static string TransactionCount(int count) =>
        count == 1 ? "1 transaction" : $"{count.ToString("N0", Culture)} transactions";
}
