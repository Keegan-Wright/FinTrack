using System.Reflection;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Encryption;

/// <summary>
/// Re-encrypts values written under an older key with the current one, recomputing blind indexes and row integrity
/// tags as it goes, and tags rows that have none yet. Run at startup after migrations, so rotating a key is to add it
/// to the keyring, make it current and restart. Old keys can be removed from the keyring once this has run.
/// </summary>
public sealed class EncryptionKeyRotator
{
    /// <summary>
    /// How many rows are re-encrypted at a time.
    /// </summary>
    private const int BatchSize = 500;

    private static readonly MethodInfo ReencryptMethod = typeof(EncryptionKeyRotator)
        .GetMethod(nameof(ReencryptAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly IDbContextFactory<FinanceTrackerContext> _dbContextFactory;
    private readonly ILogger<EncryptionKeyRotator> _logger;

    /// <param name="dbContextFactory">Makes the contexts it reads and saves through.</param>
    /// <param name="logger">Its logger.</param>
    public EncryptionKeyRotator(IDbContextFactory<FinanceTrackerContext> dbContextFactory,
        ILogger<EncryptionKeyRotator> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>How many rows were re-encrypted.</returns>
    public async Task<int> RotateAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        string currentKeyPrefix = $"{context.EncryptionService.CurrentKeyId}:";

        int total = 0;
        foreach (IEntityType entityType in context.Model.GetEntityTypes().Where(static x => !x.IsOwned()))
        {
            List<string> staleableColumns = EncryptedColumns(entityType);
            if (RowIntegrity.IsTagged(entityType))
            {
                staleableColumns.Add(RowIntegrity.TagProperty);
            }

            if (staleableColumns.Count == 0 ||
                !await HasStaleValuesAsync(context, entityType, staleableColumns, currentKeyPrefix, cancellationToken))
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
    /// <param name="entityType">The entity.</param>
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

    /// <summary>
    /// Whether any of the table's encrypted columns still has a value under an older key, or a row has no integrity
    /// tag or one under an older key.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="entityType">The entity.</param>
    /// <param name="encryptedColumns">Its encrypted columns, and its tag column when it has one.</param>
    /// <param name="currentKeyPrefix">What a value under the current key starts with.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private static async Task<bool> HasStaleValuesAsync(FinanceTrackerContext context, IEntityType entityType,
        List<string> encryptedColumns, string currentKeyPrefix, CancellationToken cancellationToken)
    {
        string table = entityType.GetSchema() is { } schema
            ? $"\"{schema}\".\"{entityType.GetTableName()}\""
            : $"\"{entityType.GetTableName()}\"";
        string stale = string.Join(" OR ", encryptedColumns.Select(static x => x == RowIntegrity.TagProperty
            ? $"(\"{x}\" IS NULL OR \"{x}\" NOT LIKE {{0}})"
            : $"(\"{x}\" <> '' AND \"{x}\" NOT LIKE {{0}})"));
        string sql = $"SELECT CASE WHEN EXISTS (SELECT 1 FROM {table} WHERE {stale}) THEN 1 ELSE 0 END AS \"Value\"";

        // Identifiers come from the model, the key prefix is a parameter
#pragma warning disable EF1002
        return await context.Database.SqlQueryRaw<int>(sql, currentKeyPrefix + "%")
            .SingleAsync(cancellationToken) == 1;
#pragma warning restore EF1002
    }

    /// <summary>
    /// Loads every row and marks its encrypted values and tag changed, so saving writes them back under the current
    /// key.
    /// </summary>
    /// <param name="keyName">The entity's single column primary key, to page by.</param>
    /// <param name="cancellationToken">Stops the work.</param>
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
                    bool isTag = property.Metadata.Name == RowIntegrity.TagProperty;
                    if (isTag || property.Metadata.FindAnnotation(Annotations.EncryptedPropertyAnnotation) is not null)
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
