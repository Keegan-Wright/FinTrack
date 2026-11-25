namespace FinanceTracker.Ai.Backup;

/// <summary>
/// The user's AI data in their backup.
/// </summary>
/// <param name="OptedIn">Whether they've turned AI on for themselves.</param>
public sealed record AiBackup(bool OptedIn);
