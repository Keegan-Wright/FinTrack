namespace FinanceTracker.Infrastructure.Common.Exports;

/// <summary>
/// What a <see cref="IBackupSection"/> puts in the backup.
/// </summary>
/// <param name="Data">The data, written as JSON.</param>
/// <param name="Records">How many records it holds, for the backup's manifest.</param>
public sealed record BackupSectionData(object Data, int Records);
