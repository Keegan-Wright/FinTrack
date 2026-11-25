namespace FinanceTracker.Infrastructure.Common.Exports;

/// <summary>
/// A module's own data in the signed in user's backup, one JSON file beside the app's. The app's backup writer writes
/// every one registered, so a module's data is never left out.
/// </summary>
public interface IBackupSection
{
    /// <summary>
    /// The file's name in the backup, unique across the app and its modules, e.g. <c>ai.json</c>.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// The signed in user's data, written as JSON.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The data and how many records it holds, for the backup's manifest.</returns>
    Task<BackupSectionData> ReadAsync(CancellationToken cancellationToken);
}
