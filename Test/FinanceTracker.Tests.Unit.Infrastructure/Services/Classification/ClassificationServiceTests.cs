using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure.Classification;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Classification;

public class ClassificationServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task AddCustomClassificationAsync_PersistsForCurrentUserAndReturnsMapped()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        ClassificationService subject = BuildSubject();

        // Act
        Contracts.Classifications.Classification added = await subject.AddCustomClassificationAsync(
            new AddClassifications { Tag = "Groceries" }, CancellationToken);

        // Assert
        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        var persisted = await context.CustomClassifications.SingleAsync(CancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(added.Tag).IsEqualTo("Groceries");
            await Assert.That(added.ClassificationId).IsEqualTo(persisted.Id);
            await Assert.That(persisted.UserId).IsEqualTo(Scenario.UserMappings[Username]);
        }
    }

    [Test]
    public async Task AddCustomClassificationAsync_DuplicateTagForUser_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Username, "Groceries"));

        ClassificationService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            subject.AddCustomClassificationAsync(new AddClassifications { Tag = "Groceries" }, CancellationToken));
    }

    [Test]
    public async Task AddCustomClassificationAsync_SameTagAsAnotherUser_IsAllowed()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.ClassificationScenario.WithClassification("theirs", OtherUsername, "Groceries"));

        ClassificationService subject = BuildSubject();

        // Act
        Contracts.Classifications.Classification added = await subject.AddCustomClassificationAsync(
            new AddClassifications { Tag = "Groceries" }, CancellationToken);

        // Assert
        await Assert.That(added.Tag).IsEqualTo("Groceries");
    }

    [Test]
    public async Task GetAllCustomClassificationsAsync_ReturnsOnlyCurrentUsersClassifications()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.ClassificationScenario.WithClassification("mine", Username, "Mine"))
            .Do(s => s.ClassificationScenario.WithClassification("theirs", OtherUsername, "Theirs"));

        ClassificationService subject = BuildSubject();

        // Act
        List<Contracts.Classifications.Classification> classifications = await subject
            .GetAllCustomClassificationsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(classifications.Select(x => x.Tag)).IsEquivalentTo(["Mine"]);
            await Assert.That(classifications.Single().ClassificationId)
                .IsEqualTo(Scenario.ClassificationMappings["mine"]);
        }
    }

    [Test]
    public async Task GetAllCustomClassificationsAsync_WhenUserHasNone_ReturnsEmpty()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        ClassificationService subject = BuildSubject();

        // Act
        List<Contracts.Classifications.Classification> classifications = await subject
            .GetAllCustomClassificationsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        await Assert.That(classifications.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetClassificationAsync_WhenOwned_ReturnsClassification()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.ClassificationScenario.WithClassification("bills", Username, "Bills"));

        ClassificationService subject = BuildSubject();
        Guid id = Scenario.ClassificationMappings["bills"];

        // Act
        GetClassification classification = await subject.GetClassificationAsync(id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(classification.ClassificationId).IsEqualTo(id);
            await Assert.That(classification.Tag).IsEqualTo("Bills");
        }
    }

    [Test]
    public async Task GetClassificationAsync_WhenOwnedByAnotherUser_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.ClassificationScenario.WithClassification("theirs", OtherUsername));

        ClassificationService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.GetClassificationAsync(Scenario.ClassificationMappings["theirs"], CancellationToken));
    }

    [Test]
    public async Task AddCustomClassificationsToTransactionAsync_AppliesOwnedClassificationsOnly()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.ClassificationScenario.WithClassification("bills", Username))
            .Do(s => s.ClassificationScenario.WithClassification("travel", Username))
            .Do(s => s.ClassificationScenario.WithClassification("theirs", OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("payment", "main"));

        ClassificationService subject = BuildSubject();

        // Act
        await subject.AddCustomClassificationsToTransactionAsync(new AddCustomClassificationsToTransaction
        {
            TransactionId = Scenario.TransactionMappings["payment"],
            Classifications =
            [
                new SelectedCustomClassification { ClassificationId = Scenario.ClassificationMappings["bills"] },
                new SelectedCustomClassification { ClassificationId = Scenario.ClassificationMappings["travel"] },
                new SelectedCustomClassification { ClassificationId = Scenario.ClassificationMappings["theirs"] }
            ]
        }, CancellationToken);

        // Assert
        BankTransaction transaction = await LoadTransactionAsync("payment");
        await Assert.That(transaction.CustomClassificationIds).IsEquivalentTo([
            Scenario.ClassificationMappings["bills"],
            Scenario.ClassificationMappings["travel"]
        ]);
    }

    [Test]
    public async Task AddCustomClassificationsToTransactionAsync_AlreadyApplied_DoesNotDuplicate()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.ClassificationScenario.WithClassification("bills", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("payment", "main"))
            .Do(s => s.TransactionScenario.WithCustomClassification("payment", "bills"));

        ClassificationService subject = BuildSubject();

        // Act
        await subject.AddCustomClassificationsToTransactionAsync(new AddCustomClassificationsToTransaction
        {
            TransactionId = Scenario.TransactionMappings["payment"],
            Classifications = [new SelectedCustomClassification { ClassificationId = Scenario.ClassificationMappings["bills"] }]
        }, CancellationToken);

        // Assert
        BankTransaction transaction = await LoadTransactionAsync("payment");
        await Assert.That(transaction.CustomClassificationIds.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddCustomClassificationsToTransactionAsync_TransactionOwnedByAnotherUser_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.ClassificationScenario.WithClassification("bills", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"))
            .Do(s => s.TransactionScenario.WithTransaction("theirs", "other"));

        ClassificationService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => subject.AddCustomClassificationsToTransactionAsync(
            new AddCustomClassificationsToTransaction
            {
                TransactionId = Scenario.TransactionMappings["theirs"],
                Classifications = [new SelectedCustomClassification { ClassificationId = Scenario.ClassificationMappings["bills"] }]
            }, CancellationToken));
    }

    [Test]
    public async Task RemoveCustomClassificationAsync_DeletesAndUnclassifiesTransactions()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.ClassificationScenario.WithClassification("bills", Username))
            .Do(s => s.ClassificationScenario.WithClassification("travel", Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("first", "main"))
            .Do(s => s.TransactionScenario.WithTransaction("second", "main"))
            .Do(s => s.TransactionScenario.WithCustomClassification("first", "bills"))
            .Do(s => s.TransactionScenario.WithCustomClassification("first", "travel"))
            .Do(s => s.TransactionScenario.WithCustomClassification("second", "bills"));

        ClassificationService subject = BuildSubject();

        // Act
        await subject.RemoveCustomClassificationAsync(Scenario.ClassificationMappings["bills"], CancellationToken);

        // Assert
        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<Guid> remainingClassifications = await context.CustomClassifications.Select(x => x.Id)
            .ToListAsync(CancellationToken);
        BankTransaction first = await LoadTransactionAsync("first");
        BankTransaction second = await LoadTransactionAsync("second");

        using (Assert.Multiple())
        {
            await Assert.That(remainingClassifications).IsEquivalentTo([Scenario.ClassificationMappings["travel"]]);
            await Assert.That(first.CustomClassificationIds).IsEquivalentTo([Scenario.ClassificationMappings["travel"]]);
            await Assert.That(second.CustomClassificationIds.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task RemoveCustomClassificationAsync_OwnedByAnotherUser_Throws()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.ClassificationScenario.WithClassification("theirs", OtherUsername));

        ClassificationService subject = BuildSubject();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.RemoveCustomClassificationAsync(Scenario.ClassificationMappings["theirs"], CancellationToken));
    }

    private async Task<BankTransaction> LoadTransactionAsync(string transactionName)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Scenario.TransactionMappings[transactionName];
        return await context.BankTransactions.SingleAsync(x => x.Id == id, CancellationToken);
    }

    private ClassificationService BuildSubject() =>
        new(UserPrincipal(Username), DbContextFactory, NullLogger<ClassificationService>.Instance);
}
