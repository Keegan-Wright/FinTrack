using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class SaveBudgetValidatorTests : UnitTestBase
{
    private readonly SaveBudgetValidator _subject = new();

    [Test]
    public async Task ValidateAsync_NameTagAndLimit_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveBudget
        {
            Name = "Groceries",
            TagIds = [Guid.CreateVersion7()],
            MonthlyLimit = 0m
        }, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_NothingFilledIn_IsInvalid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveBudget { MonthlyLimit = -1m },
            CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).IsEquivalentTo(
            [nameof(SaveBudget.Name), nameof(SaveBudget.TagIds), nameof(SaveBudget.MonthlyLimit)]);
    }

    [Test]
    public async Task ValidateAsync_UnknownRolloverMode_IsInvalid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveBudget
        {
            Name = "Groceries",
            TagIds = [Guid.CreateVersion7()],
            RolloverMode = (RolloverMode)9
        }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(SaveBudget.RolloverMode)]);
    }

    [Test]
    public async Task ValidateAsync_AnAccountWithoutTags_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveBudget
        {
            Name = "Joint account",
            AccountIds = [Guid.CreateVersion7()],
            MonthlyLimit = 1500m
        }, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_NoTagsOrAccounts_SaysToChooseOne()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveBudget
        {
            Name = "Groceries",
            MonthlyLimit = 400m
        }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .IsEquivalentTo(["Choose at least one tag or account"]);
    }

    [Test]
    public async Task ValidateAsync_AnAccountTwice_IsInvalid()
    {
        // Arrange
        Guid account = Guid.CreateVersion7();

        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveBudget
        {
            Name = "Joint account",
            AccountIds = [account, account],
            MonthlyLimit = 1500m
        }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .IsEquivalentTo(["Each account can only be chosen once"]);
    }
}
