using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

public class FinanceTrackerContextTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task EncryptedProperties_RoundTripThroughTheDatabase()
    {
        // Arrange
        DateTime goalDate = new(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("budget", Username, userId =>
                new BudgetCategory(userId, "Encrypted Budget", 77.25m, 3, 500, goalDate)));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BudgetCategory loaded = await context.BudgetCategories.SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(loaded.Name).IsEqualTo("Encrypted Budget");
            await Assert.That(loaded.AvailableFunds).IsEqualTo(77.25m);
            await Assert.That(loaded.GoalCompletionDate).IsEqualTo(goalDate);
            await Assert.That(loaded.GoalCompletionDate!.Value.Kind).IsEqualTo(DateTimeKind.Utc);
        }
    }

    [Test]
    public async Task EncryptedProperties_AreStoredAsCiphertext()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("budget", Username, userId =>
                new BudgetCategory(userId, "Secret Budget", 1, 1, 1, null)));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string stored = await context.Database
            .SqlQueryRaw<string>("SELECT \"Name\" AS \"Value\" FROM \"BudgetCategories\"")
            .SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(stored).DoesNotContain("Secret");
            await Assert.That(stored).StartsWith("1:");
            await Assert.That(ColumnEncryptionService.Decrypt<string>(stored)).IsEqualTo("Secret Budget");
        }
    }

    [Test]
    public async Task EncryptedCollectionsBytesAndEnums_RoundTripThroughTheDatabase()
    {
        // Arrange
        byte[] logo = [1, 2, 3, 255];
        DateTime occurredAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-bank", "Bank", "code", ["accounts", "balance"], logo, DateTime.UtcNow)))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("payment", "main", new TransactionDetails("ext-1", "Payment",
                TransactionType.Credit, "CREDIT", 12.34m, "GBP", occurredAt, true, ["Food", "Bills"])));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankConnection connection = await context.BankConnections.SingleAsync(CancellationToken);
        BankTransaction transaction = await context.BankTransactions.SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(connection.Logo).IsEquivalentTo(logo);
            await Assert.That(connection.Scopes).IsEquivalentTo(["accounts", "balance"]);
            await Assert.That(transaction.Type).IsEqualTo(TransactionType.Credit);
            await Assert.That(transaction.Amount).IsEqualTo(12.34m);
            await Assert.That(transaction.OccurredAt).IsEqualTo(occurredAt);
            await Assert.That(transaction.ProviderClassifications).IsEquivalentTo(["Food", "Bills"]);
        }
    }

    [Test]
    public async Task AccessTokens_AreEncryptedAndRoundTrip()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank", token: "plain-token", refreshToken: "plain-refresh"));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankConnection connection = await context.BankConnections.Include(x => x.AccessTokens).SingleAsync(CancellationToken);
        string storedToken = await context.Database
            .SqlQueryRaw<string>("SELECT \"Token\" AS \"Value\" FROM \"AccessTokens\"")
            .SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(connection.ActiveToken!.Token).IsEqualTo("plain-token");
            await Assert.That(connection.ActiveToken.RefreshToken).IsEqualTo("plain-refresh");
            await Assert.That(storedToken).IsNotEqualTo("plain-token");
        }
    }

    [Test]
    public async Task SaveChanges_StampsCreatedOnAddAndUpdatedOnModify()
    {
        // Arrange
        DateTime beforeAdd = DateTime.UtcNow;
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.DebtScenario.WithDebt("loan", Username));

        Guid debtId = Scenario.DebtMappings["loan"];

        // Act
        Debt added;
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            added = await context.Debts.SingleAsync(x => x.Id == debtId, CancellationToken);
        }

        DateTime beforeUpdate = DateTime.UtcNow;
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            Debt debt = await context.Debts.SingleAsync(x => x.Id == debtId, CancellationToken);
            debt.Rename("Renamed");
            await context.SaveChangesAsync(CancellationToken);
        }

        await using FinanceTrackerContext verificationContext = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Debt updated = await verificationContext.Debts.SingleAsync(x => x.Id == debtId, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.Created).IsGreaterThanOrEqualTo(beforeAdd);
            await Assert.That(added.Updated).IsNull();
            await Assert.That(updated.Created).IsEqualTo(added.Created);
            await Assert.That(updated.Updated).IsNotNull();
            await Assert.That(updated.Updated!.Value).IsGreaterThanOrEqualTo(beforeUpdate);
        }
    }

    [Test]
    public async Task SaveChanges_Synchronous_StampsCreatedAndUpdated()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        Guid userId = Scenario.UserMappings[Username];
        DateTime before = DateTime.UtcNow;

        // Act
        Guid debtId;
        using (FinanceTrackerContext context = DbContextFactory.CreateDbContext())
        {
            Debt debt = new(userId, "Loan", 10m, null, null);
            context.Debts.Add(debt);
            context.SaveChanges();

            debt.Rename("Renamed");
            context.SaveChanges();
            debtId = debt.Id;
        }

        // Assert
        await using FinanceTrackerContext verificationContext = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Debt loaded = await verificationContext.Debts.SingleAsync(x => x.Id == debtId, CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(loaded.Created).IsGreaterThanOrEqualTo(before);
            await Assert.That(loaded.Updated).IsNotNull();
        }
    }

    [Test]
    public async Task SyncLog_RoundTripsThroughTheDatabase()
    {
        // Arrange
        DateTime occurredAt = new(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BankAccountScenario.WithSyncLog("main", SyncType.Transactions, occurredAt));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        SyncLogEntry entry = await context.SyncLog.SingleAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(entry.AccountId).IsEqualTo(Scenario.AccountMappings["main"]);
            await Assert.That(entry.Type).IsEqualTo(SyncType.Transactions);
            await Assert.That(entry.OccurredAt.Ticks).IsEqualTo(occurredAt.Ticks);
        }
    }

    [Test]
    public async Task OwnedBy_ReturnsOnlyTheUsersAggregates()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.DebtScenario.WithDebt("mine", Username))
            .Do(s => s.DebtScenario.WithDebt("theirs", OtherUsername));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<Guid> owned = await context.Debts.OwnedBy(Scenario.UserMappings[Username])
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(owned).IsEquivalentTo([Scenario.DebtMappings["mine"]]);
    }

    [Test]
    public async Task DeletingUser_CascadesToTheirData()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BankAccountScenario.WithStandingOrders("main", new Shared.Fakers.StandingOrderDetailsFaker().Generate()))
            .Do(s => s.TransactionScenario.WithTransactions("main", 2))
            .Do(s => s.ClassificationScenario.WithClassification("tag", Username))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("budget", Username))
            .Do(s => s.DebtScenario.WithDebt("loan", Username))
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("partner", Username))
            .Do(s => s.DebtScenario.WithDebt("their-loan", OtherUsername));

        // Act
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            Guid userId = Scenario.UserMappings[Username];
            context.Users.Remove(await context.Users.SingleAsync(x => x.Id == userId, CancellationToken));
            await context.SaveChangesAsync(CancellationToken);
        }

        // Assert
        await using FinanceTrackerContext verificationContext = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(await verificationContext.BankConnections.CountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(await verificationContext.Set<AccessToken>().CountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(await verificationContext.BankAccounts.CountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(await verificationContext.BankTransactions.CountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(await verificationContext.CustomClassifications.CountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(await verificationContext.BudgetCategories.CountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(await verificationContext.HouseholdMembers.CountAsync(CancellationToken)).IsEqualTo(0);
            await Assert.That(await verificationContext.Debts.Select(x => x.Id).ToListAsync(CancellationToken))
                .IsEquivalentTo([Scenario.DebtMappings["their-loan"]]);
        }
    }

    [Test]
    public async Task BankConnections_ProviderIsUniquePerUser()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-same", "Bank", "code", [], [1], DateTime.UtcNow)));

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => Scenario.BankConnectionScenario.WithConnection("duplicate",
            Username, userId => new BankConnection(userId, "ob-same", "Bank", "code", [], [1], DateTime.UtcNow)));
    }

    [Test]
    public async Task BankConnections_SameProviderForDifferentUsers_IsAllowed()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("mine", Username, userId =>
                new BankConnection(userId, "ob-same", "Bank", "code", [], [1], DateTime.UtcNow)));

        // Act
        await Scenario.BankConnectionScenario.WithConnection("theirs", OtherUsername, userId =>
            new BankConnection(userId, "ob-same", "Bank", "code", [], [1], DateTime.UtcNow));

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That(await context.BankConnections.CountAsync(CancellationToken)).IsEqualTo(2);
    }

    [Test]
    public async Task BankTransactions_ExternalIdIsUniquePerAccount()
    {
        // Arrange
        TransactionDetails details = new Shared.Fakers.TransactionDetailsFaker().Generate();
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("first", "main", details));

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            Scenario.TransactionScenario.WithTransaction("duplicate", "main", details));
    }

    [Test]
    public async Task EncryptedCollections_ChangedInPlace_Persist()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-bank", "Bank", "code", ["accounts"], [1], DateTime.UtcNow)));

        // Act
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            BankConnection connection = await context.BankConnections.SingleAsync(CancellationToken);
            connection.Reauthenticate("code", ["accounts", "transactions"], DateTime.UtcNow);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Assert
        await using FinanceTrackerContext verificationContext = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankConnection loaded = await verificationContext.BankConnections.SingleAsync(CancellationToken);
        await Assert.That(loaded.Scopes).IsEquivalentTo(["accounts", "transactions"]);
    }

    [Test]
    public async Task EncryptedCollections_Unchanged_DoNotMarkTheAggregateModified()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("payment", "main",
                new Shared.Fakers.TransactionDetailsFaker().Generate() with { ProviderClassifications = ["Food"] }));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankConnection connection = await context.BankConnections.SingleAsync(CancellationToken);
        BankTransaction transaction = await context.BankTransactions.SingleAsync(CancellationToken);
        context.ChangeTracker.DetectChanges();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(context.Entry(connection).State).IsEqualTo(EntityState.Unchanged);
            await Assert.That(context.Entry(transaction).State).IsEqualTo(EntityState.Unchanged);
        }
    }

    [Test]
    public async Task CustomClassifications_RemovedFromTransactionCollectionPersist()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.ClassificationScenario.WithClassification("tag", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("payment", "main"))
            .Do(s => s.TransactionScenario.WithCustomClassification("payment", "tag"));

        Guid transactionId = Scenario.TransactionMappings["payment"];

        // Act
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            BankTransaction transaction = await context.BankTransactions.SingleAsync(x => x.Id == transactionId, CancellationToken);
            transaction.Unclassify(Scenario.ClassificationMappings["tag"]);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Assert
        await using FinanceTrackerContext verificationContext = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankTransaction loaded = await verificationContext.BankTransactions.SingleAsync(x => x.Id == transactionId,
            CancellationToken);

        await Assert.That(loaded.CustomClassificationIds.Count).IsEqualTo(0);
    }
}
