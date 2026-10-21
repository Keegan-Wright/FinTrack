using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

public class BlindIndexTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task Save_FillsTheBlindIndexFromTheEncryptedValue()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.ClassificationScenario.WithClassification("mine", Username, "Holidays"));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string blindIndex = await context.CustomClassifications
            .Select(static x => EF.Property<string>(x, EntityTypeBuilderExtensions.BlindIndexPropertyName(nameof(CustomClassification.Tag))))
            .SingleAsync(CancellationToken);

        // Assert
        await Assert.That(blindIndex).IsEqualTo(ColumnEncryptionService.ComputeBlindIndex("Holidays"));
    }

    [Test]
    public async Task Save_ChangedValue_RecomputesTheBlindIndex()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransactions("main", 1));

        // Act
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            BankTransaction transaction = await context.BankTransactions.SingleAsync(CancellationToken);
            context.Entry(transaction).Property(nameof(BankTransaction.ExternalId)).CurrentValue = "renamed-id";
            await context.SaveChangesAsync(CancellationToken);
        }

        // Assert
        await using FinanceTrackerContext assertContext = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string blindIndex = await assertContext.BankTransactions
            .Select(static x => EF.Property<string>(x, EntityTypeBuilderExtensions.BlindIndexPropertyName(nameof(BankTransaction.ExternalId))))
            .SingleAsync(CancellationToken);

        await Assert.That(blindIndex).IsEqualTo(ColumnEncryptionService.ComputeBlindIndex("renamed-id"));
    }

    [Test]
    public async Task UniqueIndex_SameTagForTheSameUser_IsRejected()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.ClassificationScenario.WithClassification("first", Username, "Holidays"));

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        context.CustomClassifications.Add(new CustomClassification(Scenario.UserMappings[Username], "Holidays"));

        // Act & Assert
        await Assert.That(() => context.SaveChangesAsync(CancellationToken)).Throws<DbUpdateException>();
    }

    [Test]
    public async Task UniqueIndex_SameTagForDifferentUsers_IsAllowed()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.ClassificationScenario.WithClassification("mine", Username, "Holidays"));

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        context.CustomClassifications.Add(new CustomClassification(Scenario.UserMappings[OtherUsername], "Holidays"));

        // Act & Assert
        await Assert.That(() => context.SaveChangesAsync(CancellationToken)).ThrowsNothing();
    }
}
