using FinanceTracker.Contracts;

namespace FinanceTracker.Infrastructure.OpenBanking;

/// <summary>
/// Helpers for which parts of an account a sync fetches.
/// </summary>
public static class SyncTypesExtensions
{
    extension(SyncTypes syncFlags)
    {
        /// <summary>
        /// Whether a sync asked for <paramref name="syncType"/>, either by name or as part of
        /// <see cref="SyncTypes.All"/>.
        /// </summary>
        /// <param name="syncType">What to check for.</param>
        public bool Includes(SyncTypes syncType) => syncFlags.HasFlag(SyncTypes.All) || syncFlags.HasFlag(syncType);
    }
}
