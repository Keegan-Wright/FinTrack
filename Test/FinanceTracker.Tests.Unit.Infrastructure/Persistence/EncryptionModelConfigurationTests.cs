using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

public class EncryptionModelConfigurationTests : InfrastructureTestBase
{
    [Test]
    public async Task UseEncryption_NullModelBuilder_Throws()
    {
        await Assert.That(() => ModelBuilderExtensions.UseEncryption(null!, SymmetricEncryptionService))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task UseEncryption_NullEncryptionService_Throws()
    {
        await Assert.That(() => new ModelBuilder().UseEncryption(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Encrypted_NullPropertyBuilder_Throws()
    {
        await Assert.That(() => PropertyBuilderExtensions.Encrypted<string>(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    [Arguments(typeof(BudgetCategory), nameof(BudgetCategory.Name))]
    [Arguments(typeof(BudgetCategory), nameof(BudgetCategory.AvailableFunds))]
    [Arguments(typeof(BankConnection), nameof(BankConnection.AccessCode))]
    [Arguments(typeof(BankConnection), nameof(BankConnection.Logo))]
    [Arguments(typeof(AccessToken), nameof(AccessToken.Token))]
    [Arguments(typeof(AccessToken), nameof(AccessToken.RefreshToken))]
    public async Task Model_EncryptedProperties_HaveEncryptionConverter(Type entityType, string propertyName)
    {
        // Arrange
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        // Act
        IProperty property = context.Model.FindEntityType(entityType)!.FindProperty(propertyName)!;

        // Assert
        await Assert.That(property.GetValueConverter()!.GetType().GetGenericTypeDefinition())
            .IsEqualTo(typeof(EncryptionConverter<>));
    }

    [Test]
    [Arguments(typeof(BankConnection), nameof(BankConnection.ConsentGrantedAt))]
    [Arguments(typeof(AccessToken), nameof(AccessToken.ExpiresAt))]
    [Arguments(typeof(AccessToken), nameof(AccessToken.RevokedAt))]
    public async Task Model_QueriedProperties_AreNotEncrypted(Type entityType, string propertyName)
    {
        // Arrange
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        // Act
        IProperty property = context.Model.FindEntityType(entityType)!.FindProperty(propertyName)!;

        // Assert
        await Assert.That(property.GetValueConverter()).IsNull();
    }

    [Test]
    [Arguments(typeof(BankConnection), nameof(BankConnection.Scopes))]
    [Arguments(typeof(BankTransaction), nameof(BankTransaction.ProviderClassifications))]
    public async Task Model_EncryptedCollections_CompareByElements(Type entityType, string propertyName)
    {
        // Arrange
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        ValueComparer comparer = context.Model.FindEntityType(entityType)!.FindProperty(propertyName)!.GetValueComparer();
        List<string> original = ["a", "b"];

        // Act
        object? snapshot = comparer.Snapshot(original);
        original.Add("c");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(snapshot).IsNotSameReferenceAs(original);
            await Assert.That(comparer.Equals(snapshot, original)).IsFalse();
            await Assert.That(comparer.Equals(new List<string> { "a", "b" }, new List<string> { "a", "b" })).IsTrue();
            await Assert.That(comparer.Equals(null, null)).IsTrue();
            await Assert.That(comparer.Equals(original, null)).IsFalse();
            await Assert.That(comparer.GetHashCode(new List<string> { "a", "b" }))
                .IsEqualTo(comparer.GetHashCode(new List<string> { "a", "b" }));
        }
    }

    [Test]
    public async Task Model_EncryptedNonCollections_KeepDefaultComparer()
    {
        // Arrange
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        IEntityType connection = context.Model.FindEntityType(typeof(BankConnection))!;

        // Act
        ValueComparer nameComparer = connection.FindProperty(nameof(BankConnection.Name))!.GetValueComparer();

        // Assert
        await Assert.That(nameComparer.Equals("same", "same")).IsTrue();
    }

    [Test]
    public async Task EncryptionConverter_RoundTripsValue()
    {
        // Arrange
        EncryptionConverter<decimal> converter = new(SymmetricEncryptionService);

        // Act
        object? stored = converter.ConvertToProvider(123.45m);
        object? loaded = converter.ConvertFromProvider(stored);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(stored).IsTypeOf<string>();
            await Assert.That(loaded).IsEqualTo(123.45m);
        }
    }
}
