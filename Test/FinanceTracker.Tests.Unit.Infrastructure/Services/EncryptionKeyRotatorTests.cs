using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public class EncryptionKeyRotatorTests : InfrastructureTestBase
{
    private const string Username = "primary";

    private readonly ColumnEncryptionService _rotated = new(TestEncryptionKeys.Keyring(currentKeyId: 2, 1, 2));

    [Test]
    public async Task RotateAsync_NothingUnderAnOldKey_ChangesNothing()
    {
        // Arrange
        await SeedAsync();
        string before = await ReadRawAsync("BudgetCategories", "Name");

        // Act
        int reencrypted = await new EncryptionKeyRotator(DbContextFactory, NullLogger<EncryptionKeyRotator>.Instance)
            .RotateAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reencrypted).IsEqualTo(0);
            await Assert.That(await ReadRawAsync("BudgetCategories", "Name")).IsEqualTo(before);
        }
    }

    [Test]
    public async Task RotateAsync_NewCurrentKey_ReencryptsEveryTableUnderIt()
    {
        // Arrange
        await SeedAsync();
        IDbContextFactory<FinanceTrackerContext> rotatedFactory = CreateDbContextFactory(_rotated);

        // Act
        int reencrypted = await new EncryptionKeyRotator(rotatedFactory, NullLogger<EncryptionKeyRotator>.Instance)
            .RotateAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reencrypted).IsGreaterThan(0);
            await Assert.That(await ReadRawAsync("BudgetCategories", "Name")).StartsWith("2:");
            await Assert.That(await ReadRawAsync("BankTransactions", "Amount")).StartsWith("2:");
            await Assert.That(await ReadRawAsync("BankAccounts", "Balance_Current")).StartsWith("2:");
            await Assert.That(await ReadRawAsync("StandingOrders", "Payee")).StartsWith("2:");
            await Assert.That(await ReadRawAsync("AccessTokens", "Token")).StartsWith("2:");
            await Assert.That(await ReadRawAsync("HouseholdMemberLinkedAccounts", "Number")).StartsWith("2:");
        }
    }

    [Test]
    public async Task RotateAsync_KeepsValuesAndRecomputesBlindIndexes()
    {
        // Arrange
        await SeedAsync();
        string externalId;
        DateTime occurredAt;
        await using (FinanceTrackerContext before = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            BankTransaction transaction = await before.BankTransactions.SingleAsync(CancellationToken);
            externalId = transaction.ExternalId;
            occurredAt = transaction.OccurredAt;
        }

        IDbContextFactory<FinanceTrackerContext> rotatedFactory = CreateDbContextFactory(_rotated);

        // Act
        await new EncryptionKeyRotator(rotatedFactory, NullLogger<EncryptionKeyRotator>.Instance)
            .RotateAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await rotatedFactory.CreateDbContextAsync(CancellationToken);
        BudgetCategory category = await context.BudgetCategories.SingleAsync(CancellationToken);
        string blindIndex = await context.BankTransactions
            .Select(static x => EF.Property<string>(x, EntityTypeBuilderExtensions.BlindIndexPropertyName(nameof(BankTransaction.ExternalId))))
            .SingleAsync(CancellationToken);

        string dayBlindIndex = await context.BankTransactions
            .Select(static x => EF.Property<string>(x, EntityTypeBuilderExtensions.DayBlindIndexPropertyName(nameof(BankTransaction.OccurredAt))))
            .SingleAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(category.Name).IsEqualTo("Holidays");
            await Assert.That(blindIndex).IsEqualTo(_rotated.ComputeBlindIndex(externalId));
            await Assert.That(dayBlindIndex).IsEqualTo(_rotated.ComputeBlindIndex(DayBlindIndex.Value(occurredAt)));
        }
    }

    [Test]
    public async Task RotateAsync_RunTwice_OnlyReencryptsOnce()
    {
        // Arrange
        await SeedAsync();
        EncryptionKeyRotator subject = new(CreateDbContextFactory(_rotated), NullLogger<EncryptionKeyRotator>.Instance);
        await subject.RotateAsync(CancellationToken);

        // Act
        int second = await subject.RotateAsync(CancellationToken);

        // Assert
        await Assert.That(second).IsEqualTo(0);
    }

    [Test]
    public async Task RotateAsync_MoreRowsThanOneBatch_ReencryptsThemAll()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransactions("main", 1_200));

        IDbContextFactory<FinanceTrackerContext> rotatedFactory = CreateDbContextFactory(_rotated);

        // Act
        await new EncryptionKeyRotator(rotatedFactory, NullLogger<EncryptionKeyRotator>.Instance)
            .RotateAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await rotatedFactory.CreateDbContextAsync(CancellationToken);
        int stale = await context.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM \"BankTransactions\" WHERE \"Amount\" NOT LIKE '2:%'")
            .SingleAsync(CancellationToken);

        await Assert.That(stale).IsEqualTo(0);
    }

    [Test]
    public async Task DataProtectionKeys_AreStoredEncrypted()
    {
        // Arrange
        const string keyXml = "<key id=\"1\"><secret>material</secret></key>";

        // Act
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            context.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = "key-1", Xml = keyXml });
            await context.SaveChangesAsync(CancellationToken);
        }

        // Assert
        await using FinanceTrackerContext reader = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(await ReadRawAsync("DataProtectionKeys", "Xml")).StartsWith("1:");
            await Assert.That((await reader.DataProtectionKeys.SingleAsync(CancellationToken)).Xml).IsEqualTo(keyXml);
        }
    }

    [Test]
    public async Task RotateAsync_ReencryptsDataProtectionKeys()
    {
        // Arrange, their key is an int rather than a Guid like every other table
        const string keyXml = "<key id=\"1\" />";
        await Scenario.Do(s => s.DataProtectionScenario.WithKey("key-1", keyXml));

        IDbContextFactory<FinanceTrackerContext> rotatedFactory = CreateDbContextFactory(_rotated);

        // Act
        await new EncryptionKeyRotator(rotatedFactory, NullLogger<EncryptionKeyRotator>.Instance)
            .RotateAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext reader = await rotatedFactory.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(await ReadRawAsync("DataProtectionKeys", "Xml")).StartsWith("2:");
            await Assert.That((await reader.DataProtectionKeys.SingleAsync(CancellationToken)).Xml).IsEqualTo(keyXml);
        }
    }

    private async Task SeedAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankConnectionScenario.WithAccessToken("bank"))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BankAccountScenario.WithBalance("main"))
            .Do(s => s.BankAccountScenario.WithStandingOrders("main", new StandingOrderDetailsFaker().Generate()))
            .Do(s => s.TransactionScenario.WithTransactions("main", 1))
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("partner", Username))
            .Do(s => s.HouseholdMemberScenario.WithLinkedAccount("partner", "112233", "12345678"))
            .Do(s => s.BudgetCategoryScenario.WithBudgetCategory("budget", Username, userId =>
                new BudgetCategory(userId, "Holidays", 1, 1, 1, null)));

    private async Task<string> ReadRawAsync(string table, string column)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
#pragma warning disable EF1002 // Test identifiers only
        return await context.Database
            .SqlQueryRaw<string>($"SELECT \"{column}\" AS \"Value\" FROM \"{table}\" LIMIT 1")
            .SingleAsync(CancellationToken);
#pragma warning restore EF1002
    }
}
