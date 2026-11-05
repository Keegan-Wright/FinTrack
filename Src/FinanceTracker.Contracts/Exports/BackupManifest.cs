using System.ComponentModel;

namespace FinanceTracker.Contracts.Exports;

/// <summary>
/// What a backup holds, its <c>manifest.json</c>, so it can be checked, and read back by a later version.
/// </summary>
public class BackupManifest
{
    /// <summary>
    /// What <see cref="Format"/> always is, so the file says what it is.
    /// </summary>
    public const string FormatName = "fintrack-backup";

    /// <summary>
    /// The layout's version, raised when a file or its shape changes.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>
    /// What the file is, always <see cref="FormatName"/>.
    /// </summary>
    [Description("What the file is, always fintrack-backup")]
    public string Format { get; init; } = FormatName;

    /// <summary>
    /// The layout's version, see <see cref="CurrentFormatVersion"/>.
    /// </summary>
    [Description("The version of the backup's layout")]
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    /// <summary>
    /// The version of FinTrack that made it.
    /// </summary>
    [Description("The version of FinTrack that made it")]
    public required string AppVersion { get; init; }

    /// <summary>
    /// When it was made, in UTC.
    /// </summary>
    [Description("When it was made, in UTC")]
    public DateTimeOffset ExportedAt { get; init; }

    /// <summary>
    /// Each file in the backup and how many records it holds, one for a file holding a single record.
    /// </summary>
    [Description("Each file in the backup and how many records it holds")]
    public Dictionary<string, int> Files { get; init; } = [];
}
