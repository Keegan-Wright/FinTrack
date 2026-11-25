using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// Sets a context up for encrypted columns, blind indexes and row integrity tags.
/// </summary>
public static class EncryptedContextOptionsExtensions
{
    extension(DbContextOptionsBuilder optionsBuilder)
    {
        /// <summary>
        /// Adds what an <see cref="IEncryptedContext"/> needs when it's configured. Its model also calls
        /// <see cref="ModelBuilderExtensions.UseEncryption"/> and <see cref="ModelBuilderExtensions.UseRowIntegrity"/>.
        /// </summary>
        public DbContextOptionsBuilder UseEncryptedContext()
        {
            optionsBuilder.AddInterceptors(
                BlindIndexInterceptor.Instance,
                RowIntegrityInterceptor.Instance,
                RowIntegrityCheckInterceptor.Instance,
                ComplexJsonCollectionQueryInterceptor.Instance);

            optionsBuilder.ReplaceService<IModelCacheKeyFactory, EncryptionModelCacheKeyFactory>();
#pragma warning disable EF1001 // See RowIntegrityQueryCompiler
            optionsBuilder.ReplaceService<IQueryCompiler, RowIntegrityQueryCompiler>();
#pragma warning restore EF1001
            return optionsBuilder;
        }
    }
}
