using FinanceTracker.Ai.Contracts;
using FinanceTracker.Infrastructure.Common.Exports;

namespace FinanceTracker.Ai.Backup;

/// <summary>
/// Puts the user's AI data in their backup, as <c>ai.json</c>.
/// </summary>
/// <param name="aiService">Reads the user's AI choices.</param>
public sealed class AiBackupSection(IAiService aiService) : IBackupSection
{
    /// <inheritdoc/>
    public string FileName => "ai.json";

    /// <inheritdoc/>
    public async Task<BackupSectionData> ReadAsync(CancellationToken cancellationToken)
    {
        AiStatus status = await aiService.GetStatusAsync(cancellationToken);
        return new BackupSectionData(new AiBackup(status.OptedIn), Records: 1);
    }
}
