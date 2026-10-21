namespace FinanceTracker.BackgroundJobs;

/// <summary>
/// Payload for <see cref="SyncUserOpenBankingDetailsAsync"/>, the user whose connections to sync.
/// </summary>
public sealed record SyncUserOpenBankingDetailsRequest(Guid UserId);
