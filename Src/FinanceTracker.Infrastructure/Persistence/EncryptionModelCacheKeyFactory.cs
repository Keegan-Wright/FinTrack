using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// EF caches one model per context type, and the model's value converters hold the encryption service that built
/// it. Keying on the keyring makes contexts with different keys (e.g. before and after a rotation) get
/// their own model rather than silently reusing the first one's keys.
/// </summary>
internal sealed class EncryptionModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => context is FinanceTrackerContext financeTrackerContext
        ? (context.GetType(), financeTrackerContext.EncryptionService.KeyringFingerprint, designTime)
        : (context.GetType(), designTime);
}
