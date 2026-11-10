using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// Every user's bank connections as metrics. How many are in each state, and when the one that's waited longest last
/// synced. Recorded by the jobs that sync and check consent, as they've just looked.
/// </summary>
public static class ConnectionHealthReport
{
    public static async Task RecordAsync(FinanceTrackerContext context, FinTrackMetrics metrics, DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(metrics);

        List<BankConnection> connections = await context.BankConnections.AsNoTracking()
            .ToListAsync(cancellationToken);
        CountStates(connections, metrics, now);

        metrics.OldestSyncFound(await OldestSyncAsync(context, connections, now, cancellationToken));
    }

    // Each connection once, in the first state that fits, worst first
    private static void CountStates(List<BankConnection> connections, FinTrackMetrics metrics, DateTime now)
    {
        int ok = 0;
        int renewalDue = 0;
        int lapsed = 0;
        int failing = 0;

        foreach (BankConnection connection in connections)
        {
            if (connection.HasConsentLapsed(now))
            {
                lapsed++;
            }
            else if (connection.SyncFailingSince is not null)
            {
                failing++;
            }
            else if (connection.NeedsRenewal(now))
            {
                renewalDue++;
            }
            else
            {
                ok++;
            }
        }

        metrics.ConnectionsCounted(ok, renewalDue, lapsed, failing);
    }

    /// <summary>
    /// When the connection that's waited longest last synced, of those that should be syncing: consent not lapsed,
    /// owner not locked by an admin, and synced at least once. Null when there are none.
    /// </summary>
    private static async Task<DateTimeOffset?> OldestSyncAsync(FinanceTrackerContext context,
        List<BankConnection> connections, DateTime now, CancellationToken cancellationToken)
    {
        HashSet<Guid> locked = (await context.Users
                .Where(static x => x.LockoutEnd == FinanceTrackerUser.LockedByAdminUntil)
                .Select(static x => x.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        HashSet<Guid> shouldSync = connections
            .Where(x => !x.HasConsentLapsed(now) && !locked.Contains(x.UserId))
            .Select(static x => x.Id)
            .ToHashSet();

        // Only a sync that saved leaves entries, so the latest is when it last synced
        var lastSynced = await context.SyncLog
            .Join(context.BankAccounts, static x => x.AccountId, static x => x.Id,
                static (entry, account) => new { account.ConnectionId, entry.OccurredAt })
            .GroupBy(static x => x.ConnectionId)
            .Select(static x => new { ConnectionId = x.Key, LastSyncedAt = x.Max(e => e.OccurredAt) })
            .ToListAsync(cancellationToken);

        List<DateTime> waiting = lastSynced
            .Where(x => shouldSync.Contains(x.ConnectionId))
            .Select(static x => x.LastSyncedAt)
            .ToList();

        if (waiting.Count == 0)
        {
            return null;
        }

        return new DateTimeOffset(DateTime.SpecifyKind(waiting.Min(), DateTimeKind.Utc));
    }
}
