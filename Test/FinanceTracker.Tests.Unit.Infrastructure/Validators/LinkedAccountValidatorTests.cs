using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class LinkedAccountValidatorTests : UnitTestBase
{
    private readonly LinkedAccountValidator _subject = new();

    [Test]
    [Arguments("112233", "12345678", true)]
    [Arguments("11-22-33", "1234 5678", true)]
    [Arguments(" 11 22 33 ", "12345678", true)]
    [Arguments("1122", "12345678", false)]
    [Arguments("1122330", "12345678", false)]
    [Arguments("11223a", "12345678", false)]
    [Arguments("112233", "1234567", false)]
    [Arguments("", "", false)]
    public async Task ValidateAsync_NeedsASixDigitSortCodeAndEightDigitNumber(string sortCode, string number,
        bool valid)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(
            new LinkedAccount { SortCode = sortCode, Number = number }, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsEqualTo(valid);
    }
}
