using FinanceTracker.Contracts;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// Pulls the current user's data from the open banking provider. Server only, background jobs call it, pages ask
/// for a sync through <see cref="Contracts.Services.IAutomationService.RequestSyncAsync"/>.
/// </summary>
public interface IOpenBankingSyncService
{
    /// <summary>
    /// Syncs each of the current user's connections in turn.
    /// </summary>
    Task PerformSyncAsync(SyncTypes syncFlags, CancellationToken cancellationToken);

    /// <summary>
    /// Syncs one of the current user's connections: its accounts, then each account's <paramref name="syncFlags"/>
    /// data, refreshing the access token first if it has expired. Each kind of data synced is written to the sync
    /// log.
    /// </summary>
    /// <remarks>
    /// A connection without an active token is skipped. A provider call that fails is logged and skips only that
    /// data, the rest still syncs.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The connection doesn't exist or the current user doesn't own it.</exception>
    Task SyncConnectionAsync(Guid connectionId, SyncTypes syncFlags, CancellationToken cancellationToken);
}
