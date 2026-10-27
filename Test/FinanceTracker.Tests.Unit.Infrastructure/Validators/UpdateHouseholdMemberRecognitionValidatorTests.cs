using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class UpdateHouseholdMemberRecognitionValidatorTests : UnitTestBase
{
    private readonly UpdateHouseholdMemberRecognitionValidator _subject = new();

    [Test]
    public async Task ValidateAsync_NamesAndAccounts_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new UpdateHouseholdMemberRecognition
        {
            PayeeNames = ["J Smith", "C"],
            LinkedAccounts = [new LinkedAccount { SortCode = "112233", Number = "12345678" }]
        }, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_Nothing_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new UpdateHouseholdMemberRecognition(),
            CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task ValidateAsync_BlankName_IsInvalid(string payeeName)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(
            new UpdateHouseholdMemberRecognition { PayeeNames = [payeeName] }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage)).Contains("A name can't be blank");
    }

    [Test]
    public async Task ValidateAsync_InvalidAccount_IsInvalid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new UpdateHouseholdMemberRecognition
        {
            LinkedAccounts = [new LinkedAccount { SortCode = "1122", Number = "12345678" }]
        }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage)).Contains("Sort code must be 6 digits");
    }
}
