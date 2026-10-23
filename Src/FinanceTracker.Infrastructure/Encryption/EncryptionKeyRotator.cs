using System.Reflection;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Encryption;

/// <summary>
/// Re-encrypts values written under an older key with the current one, recomputing blind indexes as it goes. Run at
/// startup after migrations, so rotating a key is: add it to the keyring, make it current, restart. Old keys can be
/// removed from the keyring once this has run.
/// </summary>
public sealed class EncryptionKeyRotator
{
    private const int BatchSize = 500;

    private static readonly MethodInfo ReencryptMethod = typeof(EncryptionKeyRotator)
        .GetMethod(nameof(ReencryptAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly ILogger<EncryptionKeyRotator> _logger;

    public EncryptionKeyRotator(IDbContextFactory<FinanceTrackerContext> dbContextFactory,
        ILogger<EncryptionKeyRotator> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <returns>How many rows were re-encrypted.</returns>
    public async Task<int> RotateAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        string currentKeyPrefix = $"{context.EncryptionService.CurrentKeyId}:";

        int total = 0;
        foreach (IEntityType entityType in context.Model.GetEntityTypes().Where(static x => !x.IsOwned()))
        {
            List<string> encryptedColumns = EncryptedColumns(entityType);
            if (encryptedColumns.Count == 0 ||
                !await HasStaleValuesAsync(context, entityType, encryptedColumns, currentKeyPrefix, cancellationToken))
            {
                continue;
            }

            string keyName = entityType.FindPrimaryKey()!.Properties.Single().Name;
            int reencrypted = await (Task<int>)ReencryptMethod.MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [keyName, cancellationToken])!;

            _logger.LogReencrypted(reencrypted, entityType.DisplayName(), context.EncryptionService.CurrentKeyId);
            total += reencrypted;
        }

        return total;
    }

    /// <summary>
    /// Encrypted columns in the entity's table, including those of owned types stored alongside it.
    /// </summary>
    private static List<string> EncryptedColumns(IEntityType entityType)
    {
        StoreObjectIdentifier table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());

        return entityType.GetProperties()
            .Concat(entityType.GetNavigations()
                .Where(static x => x.TargetEntityType.IsOwned() && !x.IsCollection)
                .SelectMany(static x => x.TargetEntityType.GetProperties()))
            .Where(static x => x.FindAnnotation(Annotations.EncryptedPropertyAnnotation) is not null)
            .Select(x => x.GetColumnName(table))
            .OfType<string>()
            .ToList();
    }

    private static async Task<bool> HasStaleValuesAsync(FinanceTrackerContext context, IEntityType entityType,
        List<string> encryptedColumns, string currentKeyPrefix, CancellationToken cancellationToken)
    {
        string table = entityType.GetSchema() is { } schema
            ? $"\"{schema}\".\"{entityType.GetTableName()}\""
            : $"\"{entityType.GetTableName()}\"";
        string stale = string.Join(" OR ",
            encryptedColumns.Select(static x => $"(\"{x}\" <> '' AND \"{x}\" NOT LIKE {{0}})"));
        string sql = $"SELECT CASE WHEN EXISTS (SELECT 1 FROM {table} WHERE {stale}) THEN 1 ELSE 0 END AS \"Value\"";

        // Identifiers come from the model, the key prefix is a parameter
#pragma warning disable EF1002
        return await context.Database.SqlQueryRaw<int>(sql, currentKeyPrefix + "%")
            .SingleAsync(cancellationToken) == 1;
#pragma warning restore EF1002
    }

    /// <summary>
    /// Loads every row and marks its encrypted values changed, so saving writes them back under the current key.
    /// </summary>
    /// <param name="keyName">The entity's single column primary key, to page by.</param>
    private async Task<int> ReencryptAsync<TEntity>(string keyName, CancellationToken cancellationToken)
        where TEntity : class
    {
        int total = 0;
        while (true)
        {
            await using FinanceTrackerContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Rewriting a row doesn't change its key, so paging by key stays stable while saving
            List<TEntity> batch = await context.Set<TEntity>()
                .OrderBy(x => EF.Property<object>(x, keyName))
                .Skip(total)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                return total;
            }

            foreach (EntityEntry entry in context.ChangeTracker.Entries())
            {
                foreach (PropertyEntry property in entry.Properties)
                {
                    if (property.Metadata.FindAnnotation(Annotations.EncryptedPropertyAnnotation) is not null)
                    {
                        property.IsModified = true;
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            total += batch.Count;
        }
    }
}
