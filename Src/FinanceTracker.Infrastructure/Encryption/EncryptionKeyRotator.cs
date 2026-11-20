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
            Dictionary<string, List<string>> staleWhere = StaleConditions(entityType);
            if (staleWhere.Count == 0 ||
                !await HasStaleValuesAsync(context, staleWhere, currentKeyPrefix, cancellationToken))
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
    /// What makes a row of <paramref name="entityType"/> stale, as SQL conditions by the table they're on. Its table
    /// and the tables of types it owns. In each, <c>{0}</c> is a value under the current key, and <c>{1}</c> and
    /// <c>{2}</c> a tag under it inside JSON.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    private static Dictionary<string, List<string>> StaleConditions(IEntityType entityType)
    {
        Dictionary<string, List<string>> byTable = [];
        AddStaleConditions(byTable, entityType, isRoot: true);
        foreach (IEntityType owned in OwnedTypes(entityType))
        {
            AddStaleConditions(byTable, owned, isRoot: false);
        }

        return byTable.Where(static x => x.Value.Count > 0).ToDictionary();
    }

    /// <summary>
    /// The types <paramref name="entityType"/> owns, and the types they own in turn.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    private static IEnumerable<IEntityType> OwnedTypes(IEntityType entityType)
    {
        foreach (INavigation navigation in entityType.GetNavigations().Where(static x => x.TargetEntityType.IsOwned()))
        {
            yield return navigation.TargetEntityType;
            foreach (IEntityType nested in OwnedTypes(navigation.TargetEntityType))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// Adds what makes <paramref name="entityType"/>'s part of a row stale. Encrypted values under an older key, and
    /// a tag that's missing or under an older key. An owned type stored in its owner's row only counts as missing its
    /// tag when it's there at all, and one stored as JSON when its JSON holds no tag under the current key.
    /// </summary>
    /// <param name="byTable">The conditions so far, by table.</param>
    /// <param name="entityType">The entity or owned type.</param>
    /// <param name="isRoot">Whether it's the entity rather than a type it owns.</param>
    private static void AddStaleConditions(Dictionary<string, List<string>> byTable, IEntityType entityType,
        bool isRoot)
    {
        if (entityType.IsMappedToJson())
        {
            AddJsonStaleCondition(byTable, entityType);
            return;
        }

        StoreObjectIdentifier table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        List<string> conditions = ConditionsFor(byTable, entityType);
        foreach (IProperty property in entityType.GetDeclaredProperties()
                     .Where(static x => x.FindAnnotation(Annotations.EncryptedPropertyAnnotation) is not null))
        {
            string column = Column(property, table);
            conditions.Add($"({column} <> '' AND {column} NOT LIKE {{0}})");
        }

        if (!RowIntegrity.IsTagged(entityType))
        {
            return;
        }

        string tag = Column(entityType.FindProperty(RowIntegrity.TagProperty)!, table);
        bool sharesOwnersRow = !isRoot &&
                               entityType.FindOwnership()!.PrincipalEntityType.GetTableName() == entityType.GetTableName();
        if (!sharesOwnersRow)
        {
            conditions.Add($"({tag} IS NULL OR {tag} NOT LIKE {{0}})");
            return;
        }

        string present = string.Join(" OR ", RowIntegrity.Covered(entityType)
            .Where(static x => !x.IsPrimaryKey())
            .Select(x => x.FindAnnotation(Annotations.EncryptedPropertyAnnotation) is not null
                ? $"{Column(x, table)} <> ''"
                : $"{Column(x, table)} IS NOT NULL"));
        conditions.Add($"({tag} NOT LIKE {{0}} OR ({tag} IS NULL AND ({present})))");
    }

    /// <summary>
    /// Adds what makes a JSON column of owned values stale. It holds some, but no tag under the current key.
    /// </summary>
    /// <param name="byTable">The conditions so far, by table.</param>
    /// <param name="entityType">The owned type stored as JSON.</param>
    private static void AddJsonStaleCondition(Dictionary<string, List<string>> byTable, IEntityType entityType)
    {
        if (!RowIntegrity.IsTagged(entityType) || entityType.FindOwnership()!.PrincipalEntityType.IsMappedToJson())
        {
            return;
        }

        // Postgres writes jsonb back with a space after each colon, SQLite as it was given
        string json = $"CAST(\"{entityType.GetContainerColumnName()}\" AS TEXT)";
        ConditionsFor(byTable, entityType).Add(
            $"({json} NOT IN ('', '[]', 'null') AND {json} NOT LIKE {{1}} AND {json} NOT LIKE {{2}})");
    }

    /// <summary>
    /// The conditions on <paramref name="entityType"/>'s table, added to <paramref name="byTable"/> when it has none
    /// yet.
    /// </summary>
    /// <param name="byTable">The conditions so far, by table.</param>
    /// <param name="entityType">The entity or owned type.</param>
    private static List<string> ConditionsFor(Dictionary<string, List<string>> byTable, IEntityType entityType)
    {
        string table = entityType.GetSchema() is { } schema
            ? $"\"{schema}\".\"{entityType.GetTableName()}\""
            : $"\"{entityType.GetTableName()}\"";
        if (!byTable.TryGetValue(table, out List<string>? conditions))
        {
            conditions = [];
            byTable[table] = conditions;
        }

        return conditions;
    }

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
        string tagInJson = $"\"{RowIntegrity.TagProperty}\":";

        // Identifiers come from the model, the key prefixes are parameters
#pragma warning disable EF1002
        return await context.Database.SqlQueryRaw<int>(sql, currentKeyPrefix + "%",
                $"%{tagInJson}\"{currentKeyPrefix}%", $"%{tagInJson} \"{currentKeyPrefix}%")
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
