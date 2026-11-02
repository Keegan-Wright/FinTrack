using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class SaveDebtValidatorTests : UnitTestBase
{
    private readonly SaveDebtValidator _subject = new();

    [Test]
    public async Task ValidateAsync_Complete_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Valid(), CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_Empty_NamesEveryMissingField()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveDebt(), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).IsEquivalentTo([
            nameof(SaveDebt.Name), nameof(SaveDebt.StartingBalance), nameof(SaveDebt.StartedOn),
            nameof(SaveDebt.MonthlyPayment)
        ]);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(101)]
    public async Task ValidateAsync_InterestRateOutOfRange_IsInvalid(int interestRate)
    {
        // Arrange
        SaveDebt debt = Valid();
        debt.InterestRate = interestRate;

        // Act
        ValidationResult result = await _subject.ValidateAsync(debt, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(SaveDebt.InterestRate)]);
    }

    [Test]
    public async Task ValidateAsync_PayOffGoalBeforeItStarted_IsInvalid()
    {
        // Arrange
        SaveDebt debt = Valid();
        debt.PayOffGoal = debt.StartedOn.AddDays(-1);

        // Act
        ValidationResult result = await _subject.ValidateAsync(debt, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(SaveDebt.PayOffGoal)]);
    }

    private static SaveDebt Valid() => new()
    {
        Name = "Car loan",
        StartingBalance = 5000m,
        StartedOn = new DateOnly(2026, 1, 1),
        InterestRate = 9.9m,
        MonthlyPayment = 199m,
        PayOffGoal = new DateOnly(2028, 1, 1)
    };
}
