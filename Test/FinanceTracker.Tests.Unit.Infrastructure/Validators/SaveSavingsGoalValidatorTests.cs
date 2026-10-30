using FinanceTracker.Contracts.SavingsGoals;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class SaveSavingsGoalValidatorTests : UnitTestBase
{
    private readonly SaveSavingsGoalValidator _subject = new();

    [Test]
    public async Task ValidateAsync_TypedIn_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveSavingsGoal { Name = "Car", Saved = 10m },
            CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_AccountWithoutOne_IsInvalid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(
            new SaveSavingsGoal { Name = "Car", Source = SavingsGoalSource.Account }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(SaveSavingsGoal.AccountId)]);
    }

    [Test]
    public async Task ValidateAsync_TagsWithoutTagsOrADay_IsInvalid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(
            new SaveSavingsGoal { Name = "Car", Source = SavingsGoalSource.Tags }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(SaveSavingsGoal.TagIds), nameof(SaveSavingsGoal.TrackedFrom)]);
    }

    [Test]
    public async Task ValidateAsync_NegativeAmounts_AreInvalid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveSavingsGoal
        {
            Name = "Car",
            Target = -1m,
            MonthlyContribution = -1m,
            Saved = -1m
        }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).IsEquivalentTo(
        [
            nameof(SaveSavingsGoal.Target), nameof(SaveSavingsGoal.MonthlyContribution),
            nameof(SaveSavingsGoal.Saved)
        ]);
    }
}
