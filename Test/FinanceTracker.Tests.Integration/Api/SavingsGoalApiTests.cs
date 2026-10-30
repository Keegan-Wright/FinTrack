using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Integration.Http;

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
        SavingsGoal added = await service.AddSavingsGoalAsync(new AddSavingsGoal
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
}
