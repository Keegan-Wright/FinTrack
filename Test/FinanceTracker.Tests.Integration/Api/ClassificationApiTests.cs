using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

namespace FinanceTracker.Tests.Integration.Api;

public class ClassificationApiTests : IntegrationTestBase
{
    [Test]
    public async Task AddCustomClassification_CanBeReadTaggedAndRemoved()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main"));

        ApiSession api = ApiFor(user);
        IClassificationService service = api.Get<IClassificationService>();
        Guid transactionId = Scenario.TransactionMappings["coffee"];

        // Act
        Classification added = await service.AddCustomClassificationAsync(new AddClassifications { Tag = "Treats" },
            CancellationToken);
        GetClassification read = await service.GetClassificationAsync(added.ClassificationId, CancellationToken);
        await service.AddCustomClassificationsToTransactionAsync(new AddCustomClassificationsToTransaction
        {
            TransactionId = transactionId,
            Classifications = [new SelectedCustomClassification { ClassificationId = added.ClassificationId }]
        }, CancellationToken);
        List<Transaction> tagged = await api.Get<ITransactionsService>()
            .GetAllTransactionsAsync(new FilterTransactions { Tags = ["Treats"] }, CancellationToken)
            .ToListAsync(CancellationToken);
        await service.RemoveCustomClassificationAsync(added.ClassificationId, CancellationToken);
        List<Classification> afterRemove = await service.GetAllCustomClassificationsAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(read.Tag).IsEqualTo("Treats");
            await Assert.That(tagged.Select(x => x.TransactionId)).IsEquivalentTo([transactionId]);
            await Assert.That(afterRemove).IsEmpty();
        }
    }

    [Test]
    public async Task RenameCustomClassification_IsSavedAndListed()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IClassificationService service = ApiFor(user).Get<IClassificationService>();
        Classification added = await service.AddCustomClassificationAsync(new AddClassifications { Tag = "Holiday" },
            CancellationToken);

        // Act
        Classification renamed = await service.RenameCustomClassificationAsync(added.ClassificationId,
            new RenameClassification { Tag = "Travel" }, CancellationToken);
        List<Classification> listed = await service.GetAllCustomClassificationsAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(renamed.Tag).IsEqualTo("Travel");
            await Assert.That(listed.Single().Tag).IsEqualTo("Travel");
        }
    }

    [Test]
    public async Task AddCustomClassification_ThatTheyAlreadyHave_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IClassificationService service = ApiFor(user).Get<IClassificationService>();
        await service.AddCustomClassificationAsync(new AddClassifications { Tag = "Holiday" }, CancellationToken);

        // Act
        NameTakenException? exception = await Assert.ThrowsAsync<NameTakenException>(() =>
            service.AddCustomClassificationAsync(new AddClassifications { Tag = "holiday" }, CancellationToken));

        // Assert
        await Assert.That(exception!.Message).IsEqualTo("You already have a tag called holiday");
    }

    [Test]
    public async Task RenameCustomClassification_ToOneTheyAlreadyHave_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IClassificationService service = ApiFor(user).Get<IClassificationService>();
        Classification holiday = await service.AddCustomClassificationAsync(new AddClassifications { Tag = "Holiday" },
            CancellationToken);
        await service.AddCustomClassificationAsync(new AddClassifications { Tag = "Travel" }, CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<NameTakenException>(() => service.RenameCustomClassificationAsync(
            holiday.ClassificationId, new RenameClassification { Tag = "Travel" }, CancellationToken));
    }

    [Test]
    public async Task RenameCustomClassification_OfAnotherUser_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        Classification theirs = await ApiFor(owner).Get<IClassificationService>()
            .AddCustomClassificationAsync(new AddClassifications { Tag = "Holiday" }, CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => ApiFor(other).Get<IClassificationService>()
            .RenameCustomClassificationAsync(theirs.ClassificationId, new RenameClassification { Tag = "Mine" },
                CancellationToken));
    }

    [Test]
    public async Task GetClassification_ThatDoesNotExist_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IClassificationService service = ApiFor(user).Get<IClassificationService>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetClassificationAsync(Guid.NewGuid(), CancellationToken));
    }

    [Test]
    public async Task AddCustomClassificationsToTransaction_OfAnotherUser_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", owner.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("theirs", "main"));

        IClassificationService service = ApiFor(other).Get<IClassificationService>();
        Classification mine = await service.AddCustomClassificationAsync(new AddClassifications { Tag = "Mine" },
            CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddCustomClassificationsToTransactionAsync(new AddCustomClassificationsToTransaction
            {
                TransactionId = Scenario.TransactionMappings["theirs"],
                Classifications = [new SelectedCustomClassification { ClassificationId = mine.ClassificationId }]
            }, CancellationToken));
    }
}
