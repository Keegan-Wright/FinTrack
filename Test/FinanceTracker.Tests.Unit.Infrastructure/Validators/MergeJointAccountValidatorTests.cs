using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Validation;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class MergeJointAccountValidatorTests
{
    private readonly MergeJointAccountValidator _validator = new();

    [Test]
    public async Task Validate_BothCopies_Passes()
    {
        // Arrange
        MergeJointAccount request = new() { YourAccountId = Guid.NewGuid(), TheirAccountId = Guid.NewGuid() };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_NoCopyOfYours_Fails()
    {
        // Arrange
        MergeJointAccount request = new() { YourAccountId = Guid.Empty, TheirAccountId = Guid.NewGuid() };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .Contains("Choose your copy of the account");
    }

    [Test]
    public async Task Validate_NoCopyOfTheirs_Fails()
    {
        // Arrange
        MergeJointAccount request = new() { YourAccountId = Guid.NewGuid(), TheirAccountId = Guid.Empty };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .Contains("Choose their copy of the account");
    }
}
