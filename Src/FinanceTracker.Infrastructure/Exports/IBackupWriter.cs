using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Infrastructure.Exports;

/// <summary>
/// Writes a backup of everything the user has as a zip, see <see cref="IExportsService.WriteBackupAsync"/>.
/// </summary>
public interface IBackupWriter
{
    /// <summary>
    /// Writes the backup zip to <paramref name="destination"/>.
    /// </summary>
    Task WriteAsync(Stream destination, CancellationToken cancellationToken);
}
