using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class RenameClassificationValidatorTests : UnitTestBase
{
    private readonly RenameClassificationValidator _subject = new();

    [Test]
    public async Task ValidateAsync_NewTag_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new RenameClassification { Tag = "Travel" },
            CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public async Task ValidateAsync_NoTag_SaysItsRequired(string tag)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new RenameClassification { Tag = tag },
            CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage)).IsEquivalentTo(
            ["Classification is required"]);
    }
}
