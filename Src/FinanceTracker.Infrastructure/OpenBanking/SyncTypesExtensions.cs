using FinanceTracker.Contracts;

namespace FinanceTracker.Infrastructure.OpenBanking;

public static class SyncTypesExtensions
{
    extension(SyncTypes syncFlags)
    {
        /// <summary>
        /// Whether a sync asked for <paramref name="syncType"/>, either by name or as part of
        /// <see cref="SyncTypes.All"/>.
        /// </summary>
        public bool Includes(SyncTypes syncType) => syncFlags.HasFlag(SyncTypes.All) || syncFlags.HasFlag(syncType);
    }
}
