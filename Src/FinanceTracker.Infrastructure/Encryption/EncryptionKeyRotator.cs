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
        foreach (IEntityType entityType in context.Model.GetEntityTypes())
        {
            Dictionary<string, List<string>> staleWhere = StaleConditions(entityType);
            if (staleWhere.Count == 0 ||
                !await HasStaleValuesAsync(context, staleWhere, currentKeyPrefix, cancellationToken))
            {
                continue;
            }

            List<string> keyNames = entityType.FindPrimaryKey()!.Properties.Select(static x => x.Name).ToList();
            int reencrypted = await (Task<int>)ReencryptMethod.MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [keyNames, cancellationToken])!;

            _logger.LogReencrypted(reencrypted, entityType.DisplayName(), context.EncryptionService.CurrentKeyId);
            total += reencrypted;
        }

        return total;
    }

    /// <summary>
    /// What makes a row of <paramref name="entityType"/> stale, as SQL conditions on its table. Encrypted values under
    /// an older key, in its columns or its complex types, and a tag that's missing, from an older format or under an
    /// older key. <c>{0}</c> matches a value under the current key, <c>{1}</c> a tag under it at the current format,
    /// and <c>{2}</c> is what a value under the current key starts with.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    private static Dictionary<string, List<string>> StaleConditions(IEntityType entityType)
    {
        StoreObjectIdentifier table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        List<string> conditions = [];
        foreach (IProperty property in entityType.GetDeclaredProperties().Where(RowIntegrity.IsEncrypted))
        {
            string column = Column(property, table);
            conditions.Add($"({column} <> '' AND {column} NOT LIKE {{0}})");
        }

        AddComplexStaleConditions(conditions, entityType, table);

        if (RowIntegrity.IsTagged(entityType))
        {
            string tag = Column(entityType.FindProperty(RowIntegrity.TagProperty)!, table);
            conditions.Add($"({tag} IS NULL OR {tag} NOT LIKE {{1}})");
        }

        if (conditions.Count == 0)
        {
            return [];
        }

        return new Dictionary<string, List<string>> { [QuotedTable(entityType)] = conditions };
    }

    /// <summary>
    /// Adds what makes the values in <paramref name="type"/>'s complex types stale. One stored in its owner's columns
    /// is checked column by column. One stored as JSON is stale when it holds more encrypted values than it holds
    /// under the current key, counted property by property, as EF can update part of a JSON value.
    /// </summary>
    /// <param name="conditions">The conditions on the table.</param>
    /// <param name="type">The entity or complex type.</param>
    /// <param name="table">The table.</param>
    private static void AddComplexStaleConditions(List<string> conditions, ITypeBase type, StoreObjectIdentifier table)
    {
        foreach (IComplexProperty complexProperty in RowIntegrity.CoveredComplex(type))
        {
            IComplexType complexType = complexProperty.ComplexType;
            if (!complexType.IsMappedToJson())
            {
                foreach (IProperty property in complexType.GetProperties().Where(RowIntegrity.IsEncrypted))
                {
                    string column = Column(property, table);
                    conditions.Add($"({column} <> '' AND {column} NOT LIKE {{0}})");
                }

                AddComplexStaleConditions(conditions, complexType, table);
                continue;
            }

            string json = $"CAST(\"{complexType.GetContainerColumnName()}\" AS TEXT)";
            foreach (string name in EncryptedJsonNames(complexType))
            {
                conditions.Add($"({Occurrences(json, $"'\"{name}\":\"'")} + {Occurrences(json, $"'\"{name}\": \"'")}" +
                               $" <> {Occurrences(json, $"'\"{name}\":\"' || {{2}}")}" +
                               $" + {Occurrences(json, $"'\"{name}\": \"' || {{2}}")})");
            }
        }
    }

    /// <summary>
    /// The JSON names of the encrypted properties in a complex type stored as JSON, and in the complex types it holds.
    /// </summary>
    /// <param name="complexType">The complex type.</param>
    private static IEnumerable<string> EncryptedJsonNames(IComplexType complexType) =>
        complexType.GetProperties().Where(RowIntegrity.IsEncrypted)
            .Select(static x => x.GetJsonPropertyName() ?? x.Name)
            .Concat(RowIntegrity.CoveredComplex(complexType).SelectMany(static x => EncryptedJsonNames(x.ComplexType)))
            .Distinct();

    /// <summary>
    /// SQL counting how often <paramref name="pattern"/> appears in <paramref name="text"/>. Postgres writes jsonb
    /// back with a space after each colon, SQLite as it was given, so both are counted by the caller.
    /// </summary>
    /// <param name="text">The SQL text being searched.</param>
    /// <param name="pattern">The SQL text being counted.</param>
    private static string Occurrences(string text, string pattern) =>
        $"((LENGTH({text}) - LENGTH(REPLACE({text}, {pattern}, ''))) / LENGTH({pattern}))";

    /// <summary>
    /// <paramref name="entityType"/>'s table, quoted, with its schema when it has one.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    private static string QuotedTable(IEntityType entityType) =>
        entityType.GetSchema() is { } schema
            ? $"\"{schema}\".\"{entityType.GetTableName()}\""
            : $"\"{entityType.GetTableName()}\"";

    /// <summary>
    /// A property's column in <paramref name="table"/>, quoted.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="table">The table.</param>
    private static string Column(IProperty property, StoreObjectIdentifier table) =>
        $"\"{property.GetColumnName(table)}\"";

    /// <summary>
    /// Whether any row in any of the tables meets one of its stale conditions.
    /// </summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="staleWhere">The conditions, by table.</param>
    /// <param name="currentKeyPrefix">What a value under the current key starts with.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private static async Task<bool> HasStaleValuesAsync(FinanceTrackerContext context,
        Dictionary<string, List<string>> staleWhere, string currentKeyPrefix, CancellationToken cancellationToken)
    {
        string exists = string.Join(" OR ", staleWhere.Select(static x =>
            $"EXISTS (SELECT 1 FROM {x.Key} WHERE {string.Join(" OR ", x.Value)})"));
        string sql = $"SELECT CASE WHEN {exists} THEN 1 ELSE 0 END AS \"Value\"";
        string currentTagPrefix = RowIntegrity.TagPrefix(context.EncryptionService.CurrentKeyId);

        // Identifiers come from the model, the key prefixes are parameters
#pragma warning disable EF1002
        return await context.Database.SqlQueryRaw<int>(sql, currentKeyPrefix + "%", currentTagPrefix + "%",
                currentKeyPrefix)
            .SingleAsync(cancellationToken) == 1;
#pragma warning restore EF1002
    }

    /// <summary>
    /// <paramref name="rows"/> in the order of their key, column by column.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="keyNames">The key's columns.</param>
    private static IOrderedQueryable<TEntity> OrderedByKey<TEntity>(IQueryable<TEntity> rows, List<string> keyNames)
        where TEntity : class
    {
        IOrderedQueryable<TEntity> ordered = rows.OrderBy(x => EF.Property<object>(x, keyNames[0]));
        foreach (string keyName in keyNames.Skip(1))
        {
            ordered = ordered.ThenBy(x => EF.Property<object>(x, keyName));
        }

        return ordered;
    }

    /// <summary>
    /// Marks the encrypted values in complex types changed. One stored as JSON is written back whole, one stored in
    /// its owner's columns column by column.
    /// </summary>
    /// <param name="complexProperties">The single complex values.</param>
    /// <param name="complexCollections">The lists of complex values.</param>
    private static void MarkEncryptedModified(IEnumerable<ComplexPropertyEntry> complexProperties,
        IEnumerable<ComplexCollectionEntry> complexCollections)
    {
        foreach (ComplexPropertyEntry complexProperty in complexProperties)
        {
            if (complexProperty.CurrentValue is null ||
                !RowIntegrity.CoveredComplex(complexProperty.Metadata.DeclaringType).Contains(complexProperty.Metadata))
            {
                continue;
            }

            if (complexProperty.Metadata.ComplexType.IsMappedToJson())
            {
                complexProperty.IsModified = true;
                continue;
            }

            foreach (PropertyEntry property in complexProperty.Properties.Where(x => RowIntegrity.IsEncrypted(x.Metadata)))
            {
                property.IsModified = true;
            }

            MarkEncryptedModified(complexProperty.ComplexProperties, complexProperty.ComplexCollections);
        }

        foreach (ComplexCollectionEntry complexCollection in complexCollections)
        {
            if (complexCollection.CurrentValue is not null &&
                RowIntegrity.CoveredComplex(complexCollection.Metadata.DeclaringType).Contains(complexCollection.Metadata))
            {
                complexCollection.IsModified = true;
            }
        }
    }

    /// <summary>
    /// Loads every row and marks its encrypted values and tag changed, so saving writes them back under the current
    /// key.
    /// </summary>
    /// <param name="keyNames">The entity's primary key columns, to page by. Identity's tokens have three.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    private async Task<int> ReencryptAsync<TEntity>(List<string> keyNames, CancellationToken cancellationToken)
        where TEntity : class
    {
        int total = 0;
        while (true)
        {
            await using FinanceTrackerContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Rewriting a row doesn't change its key, so paging by key stays stable while saving
            List<TEntity> batch = await OrderedByKey(context.Set<TEntity>(), keyNames)
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
                    if (isTag || RowIntegrity.IsEncrypted(property.Metadata))
                    {
                        property.IsModified = true;
                    }
                }

                MarkEncryptedModified(entry.ComplexProperties, entry.ComplexCollections);
            }

            await context.SaveChangesAsync(cancellationToken);
            total += batch.Count;
        }
    }
}
