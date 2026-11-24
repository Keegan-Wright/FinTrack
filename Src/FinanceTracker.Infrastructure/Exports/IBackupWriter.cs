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
    /// <param name="destination">Where the zip is written.</param>
    /// <param name="password">The password every file is encrypted with, AES-256, or null for a plain zip.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    Task WriteAsync(Stream destination, string? password, CancellationToken cancellationToken);
}
