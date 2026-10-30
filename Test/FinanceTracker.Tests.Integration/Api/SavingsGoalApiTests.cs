using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

namespace FinanceTracker.Tests.Integration.Api;

public class SavingsGoalApiTests : IntegrationTestBase
{
    [Test]
    public async Task AddSavingsGoal_IsListedAndCanBeDeleted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ISavingsGoalsService service = ApiFor(user).Get<ISavingsGoalsService>();
        DateTime goalDate = new(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        SavingsGoal added = await service.AddSavingsGoalAsync(new SaveSavingsGoal
        {
            Name = "Holiday",
            Saved = 100m,
            MonthlyContribution = 25m,
            Target = 1000m,
            TargetDate = goalDate
        }, CancellationToken);
        List<SavingsGoal> listed = await service.GetSavingsGoalsAsync(CancellationToken).ToListAsync(CancellationToken);
        bool deleted = await service.DeleteSavingsGoalAsync(added.Id, CancellationToken);
        bool deletedAgain = await service.DeleteSavingsGoalAsync(added.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.Name).IsEqualTo("Holiday");
            await Assert.That(added.Target).IsEqualTo(1000m);
            await Assert.That(listed.Single().TargetDate).IsEqualTo(goalDate);
            await Assert.That(deleted).IsTrue();
            await Assert.That(deletedAgain).IsFalse();
        }
    }

    [Test]
    public async Task TagGoal_CountsTaggedTransactions_AndCanBeEditedToTypedIn()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("paid-in", "savings",
                new TransactionDetailsFaker().Generate() with
                {
                    Amount = 250m,
                    Type = TransactionType.Credit,
                    OccurredAt = DateTime.UtcNow.AddDays(-1)
                }));

        ApiSession api = ApiFor(user);
        ISavingsGoalsService service = api.Get<ISavingsGoalsService>();
        Classification car = await api.Get<IClassificationService>()
            .AddCustomClassificationAsync(new AddClassifications { Tag = "Car fund" }, CancellationToken);
        await api.Get<IClassificationService>().AddCustomClassificationsToTransactionAsync(
            new AddCustomClassificationsToTransaction
            {
                TransactionId = Scenario.TransactionMappings["paid-in"],
                Classifications = [new SelectedCustomClassification { ClassificationId = car.ClassificationId }]
            }, CancellationToken);

        // Act
        SavingsGoal added = await service.AddSavingsGoalAsync(new SaveSavingsGoal
        {
            Name = "Car",
            Target = 10000m,
            Source = SavingsGoalSource.Tags,
            Saved = 100m,
            TagIds = [car.ClassificationId],
            TrackedFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))
        }, CancellationToken);
        SavingsGoal edited = await service.UpdateSavingsGoalAsync(added.Id,
            new SaveSavingsGoal { Name = "Car", Target = 10000m, Saved = 400m }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(added.Saved).IsEqualTo(350m);
            await Assert.That(edited.Source).IsEqualTo(SavingsGoalSource.Manual);
            await Assert.That(edited.Saved).IsEqualTo(400m);
        }
    }

    [Test]
    public async Task GetSavingsGoal_OfAnotherUser_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        SavingsGoal goal = await ApiFor(owner).Get<ISavingsGoalsService>()
            .AddSavingsGoalAsync(new SaveSavingsGoal { Name = "Mine" }, CancellationToken);

        ISavingsGoalsService service = ApiFor(other).Get<ISavingsGoalsService>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetSavingsGoalAsync(goal.Id, CancellationToken));
    }
}
