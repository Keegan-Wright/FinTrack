using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class SplitTransactionValidatorTests : UnitTestBase
{
    private readonly SplitTransactionValidator _subject = new();

    [Test]
    public async Task ValidateAsync_TwoParts_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Parts(2), CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(SplitTransaction.MaxParts + 1)]
    public async Task ValidateAsync_TooFewOrTooManyParts_IsInvalid(int count)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Parts(count), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(SplitTransaction.Parts)]);
    }

    [Test]
    public async Task ValidateAsync_AZeroPartOrALongNote_IsInvalid()
    {
        // Arrange
        SplitTransaction request = Parts(2);
        request.Parts[0].Amount = 0;
        request.Parts[1].Note = new string('a', SplitTransaction.MaxNoteLength + 1);

        // Act
        ValidationResult result = await _subject.ValidateAsync(request, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo(["Parts[0].Amount", "Parts[1].Note"]);
    }

    private static SplitTransaction Parts(int count) => new()
    {
        Parts = Enumerable.Range(0, count).Select(static _ => new SplitTransactionPart { Amount = -10m }).ToList()
    };
}
