using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Tests.Integration.Persistence;

/// <summary>
/// Behaviour that depends on the real Postgres provider, which the SQLite unit tests can't cover.
/// </summary>
public class PostgresPersistenceTests : IntegrationTestBase
{
    [Test]
    public async Task DataProtectionKeys_ArePersistedEncrypted()
    {
        // Arrange, signing in protects the cookie, so the app has created its key by now
        await RegisterUserAsync();

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<string> stored = await context.Database
            .SqlQueryRaw<string>("SELECT \"Xml\" AS \"Value\" FROM \"DataProtectionKeys\"")
            .ToListAsync(CancellationToken);
        List<string?> decrypted = await context.DataProtectionKeys.Select(x => x.Xml).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(stored).IsNotEmpty();
            await Assert.That(stored.All(x => x.StartsWith($"{FinTrackAppFixture.EncryptionConfiguration.CurrentKeyId}:", StringComparison.Ordinal))).IsTrue();
            await Assert.That(stored.Any(x => x.Contains("<key", StringComparison.Ordinal))).IsFalse();
            await Assert.That(decrypted.All(x => x!.StartsWith("<key", StringComparison.Ordinal))).IsTrue();
        }
    }

    [Test]
    public async Task EncryptedColumns_AreNotStoredAsPlaintext()
    {
        // Arrange
        string username = UniqueName();
        const string connectionName = "Plaintext Bank Name";
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username, userId =>
                new BankConnection(userId, UniqueName("provider"), connectionName, "access-code", ["accounts"], [1, 2, 3], DateTime.UtcNow)));

        Guid connectionId = Scenario.ConnectionMappings["bank"];

        // Act
        string stored = await ReadRawColumnAsync<BankConnection>(nameof(BankConnection.Name), connectionId);

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankConnection loaded = await context.BankConnections.SingleAsync(x => x.Id == connectionId, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(stored).IsNotEqualTo(connectionName);
            await Assert.That(stored).DoesNotContain("Plaintext");
            await Assert.That(loaded.Name).IsEqualTo(connectionName);
        }
    }

    [Test]
    public async Task EncryptedColumns_SameValueIsStoredDifferently()
    {
        // Arrange
        string username = UniqueName();
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("first", username, userId =>
                new BudgetCategory(userId, "Holidays", 1, 1, 1, null)))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("second", username, userId =>
                new BudgetCategory(userId, "Holidays", 1, 1, 1, null)));

        // Act
        string first = await ReadRawColumnAsync<BudgetCategory>(nameof(BudgetCategory.Name),
            Scenario.BudgetCategoryMappings["first"]);
        string second = await ReadRawColumnAsync<BudgetCategory>(nameof(BudgetCategory.Name),
            Scenario.BudgetCategoryMappings["second"]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first).IsNotEqualTo(second);
            await Assert.That(first).StartsWith("1:");
        }
    }

    [Test]
    public async Task EncryptedCollection_ChangesAreSaved()
    {
        // Arrange
        string username = UniqueName();
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username));

        Guid connectionId = Scenario.ConnectionMappings["bank"];

        // Act
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            BankConnection connection = await context.BankConnections.SingleAsync(x => x.Id == connectionId, CancellationToken);
            connection.Reauthenticate("new-access-code", ["info", "accounts"], DateTime.UtcNow);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Assert
        await using FinanceTrackerContext assertContext = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankConnection reloaded = await assertContext.BankConnections.SingleAsync(x => x.Id == connectionId, CancellationToken);

        await Assert.That(reloaded.Scopes).IsEquivalentTo(["info", "accounts"]);
    }

    [Test]
    public async Task ConcurrentUpdates_ToTheSameAggregate_AreRejected()
    {
        // Arrange
        string username = UniqueName();
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("groceries", username, userId =>
                new BudgetCategory(userId, "Groceries", 100, 100, 0, null)));

        Guid categoryId = Scenario.BudgetCategoryMappings["groceries"];

        await using FinanceTrackerContext first = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await using FinanceTrackerContext second = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        BudgetCategory firstCopy = await first.BudgetCategories.SingleAsync(x => x.Id == categoryId, CancellationToken);
        BudgetCategory secondCopy = await second.BudgetCategories.SingleAsync(x => x.Id == categoryId, CancellationToken);

        firstCopy.Rename("Food");
        await first.SaveChangesAsync(CancellationToken);

        secondCopy.Rename("Shopping");

        // Act & Assert
        await Assert.That(() => second.SaveChangesAsync(CancellationToken)).Throws<DbUpdateConcurrencyException>();
    }

    [Test]
    public async Task Connections_AreUniquePerUserAndProvider()
    {
        // Arrange
        string username = UniqueName();
        string providerId = UniqueName("provider");
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username, userId =>
                new BankConnection(userId, providerId, "Bank", "access-code", ["accounts"], [], DateTime.UtcNow)));

        Guid userId = Scenario.UserMappings[username];

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        context.BankConnections.Add(new BankConnection(userId, providerId, "Same Bank", "other-code", ["accounts"], [], DateTime.UtcNow));

        // Act & Assert
        await Assert.That(() => context.SaveChangesAsync(CancellationToken)).Throws<DbUpdateException>();
    }

    [Test]
    public async Task DeletingAConnection_RemovesItsAccountsAndTransactions()
    {
        // Arrange
        string username = UniqueName();
        await Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransactions("main", 3));

        Guid connectionId = Scenario.ConnectionMappings["bank"];
        Guid accountId = Scenario.AccountMappings["main"];

        // Act
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            BankConnection connection = await context.BankConnections.SingleAsync(x => x.Id == connectionId, CancellationToken);
            context.BankConnections.Remove(connection);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Assert
        await using FinanceTrackerContext assertContext = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(await assertContext.BankAccounts.AnyAsync(x => x.Id == accountId, CancellationToken)).IsFalse();
            await Assert.That(await assertContext.BankTransactions.AnyAsync(x => x.AccountId == accountId, CancellationToken)).IsFalse();
        }
    }

    /// <summary>
    /// Reads a column as it is stored, bypassing the model's value converters.
    /// </summary>
    private async Task<string> ReadRawColumnAsync<TEntity>(string propertyName, Guid id)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        IEntityType entityType = context.Model.FindEntityType(typeof(TEntity))!;
        StoreObjectIdentifier table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        string column = entityType.FindProperty(propertyName)!.GetColumnName(table)!;
        string idColumn = entityType.FindProperty("Id")!.GetColumnName(table)!;
        string tableName = table.Schema is null ? $"\"{table.Name}\"" : $"\"{table.Schema}\".\"{table.Name}\"";

#pragma warning disable EF1002 // Identifiers come from the model, the id is parameterised
        return await context.Database
            .SqlQueryRaw<string>($"SELECT \"{column}\" AS \"Value\" FROM {tableName} WHERE \"{idColumn}\" = {{0}}", id)
            .SingleAsync(CancellationToken);
#pragma warning restore EF1002
    }
}
