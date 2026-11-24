using System.IO.Compression;

namespace FinanceTracker.Infrastructure.Exports;

/// <summary>
/// The backup's zip, adding each file plain or encrypted with AES-256 under the one password.
/// </summary>
internal sealed class BackupZip
{
    private readonly ZipArchive _archive;
    private readonly string? _password;

    /// <param name="archive">The zip being written.</param>
    /// <param name="password">The password every file is encrypted with, or null for a plain zip.</param>
    public BackupZip(ZipArchive archive, string? password)
    {
        _archive = archive;
        _password = password;
    }

    /// <summary>
    /// Adds a file named <paramref name="name"/> and opens it to write.
    /// </summary>
    /// <param name="name">The file's name in the zip, which is never encrypted.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    public Task<Stream> OpenEntryAsync(string name, CancellationToken cancellationToken)
    {
        if (_password is null)
        {
            return _archive.CreateEntry(name, CompressionLevel.Optimal).OpenAsync(cancellationToken);
        }

        // AES-256 in WinZip's format, which 7-Zip and the like open
        return _archive.CreateEntry(name, CompressionLevel.Optimal, _password, ZipEncryptionMethod.Aes256)
            .OpenAsync(cancellationToken);
    }
}
