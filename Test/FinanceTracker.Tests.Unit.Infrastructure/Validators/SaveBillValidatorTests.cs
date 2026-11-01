using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class SaveBillValidatorTests : UnitTestBase
{
    private readonly SaveBillValidator _subject = new();

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
        ValidationResult result = await _subject.ValidateAsync(new SaveBill(), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).IsEquivalentTo([
            nameof(SaveBill.Name), nameof(SaveBill.Payee), nameof(SaveBill.AccountId), nameof(SaveBill.NextDue),
            nameof(SaveBill.ExpectedAmount)
        ]);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(101)]
    public async Task ValidateAsync_ToleranceOutOfRange_IsInvalid(int tolerance)
    {
        // Arrange
        SaveBill bill = Valid();
        bill.TolerancePercent = tolerance;

        // Act
        ValidationResult result = await _subject.ValidateAsync(bill, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(SaveBill.TolerancePercent)]);
    }

    [Test]
    public async Task ValidateAsync_UnknownFrequency_IsInvalid()
    {
        // Arrange
        SaveBill bill = Valid();
        bill.Frequency = (BillFrequency)99;

        // Act
        ValidationResult result = await _subject.ValidateAsync(bill, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(SaveBill.Frequency)]);
    }

    private static SaveBill Valid() => new()
    {
        Name = "Streaming",
        Payee = "Streamco",
        AccountId = Guid.CreateVersion7(),
        Frequency = BillFrequency.Monthly,
        NextDue = new DateOnly(2026, 11, 12),
        ExpectedAmount = 9.99m,
        TolerancePercent = 5
    };
}
