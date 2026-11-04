namespace FinanceTracker.Contracts.Api;

/// <summary>
/// What an export's file is called, the same whether the server names the download or the browser saves it.
/// </summary>
public static class ExportFileNames
{
    /// <summary>
    /// e.g. <c>fintrack-transactions-2026-11-04.csv</c>.
    /// </summary>
    public static string Transactions(DateTimeOffset now) => $"fintrack-transactions-{now:yyyy-MM-dd}.csv";

    /// <summary>
    /// e.g. <c>fintrack-backup-2026-11-04.zip</c>.
    /// </summary>
    public static string Backup(DateTimeOffset now) => $"fintrack-backup-{now:yyyy-MM-dd}.zip";
}
