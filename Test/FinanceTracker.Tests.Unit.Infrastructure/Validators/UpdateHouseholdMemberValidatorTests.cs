using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class UpdateHouseholdMemberValidatorTests : UnitTestBase
{
    private readonly UpdateHouseholdMemberValidator _subject = new();

    [Test]
    public async Task ValidateAsync_NamesAndNoIncome_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(
            new UpdateHouseholdMember { FirstName = "Jane", LastName = "Smith", YearlyIncome = 0 }, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_MissingNamesAndNegativeIncome_SaysWhatsWrong()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(
            new UpdateHouseholdMember { FirstName = "", LastName = "", YearlyIncome = -1 }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage)).IsEquivalentTo(
            ["First Name is required", "Last Name is required", "Yearly income must be 0 or more"]);
    }
}
