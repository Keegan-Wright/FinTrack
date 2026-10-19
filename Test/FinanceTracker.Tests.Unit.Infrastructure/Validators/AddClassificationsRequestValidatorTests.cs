using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Infrastructure.Validation;
using FinanceTracker.Tests.Shared;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class AddClassificationsRequestValidatorTests : TestFixtureBase
{
    private readonly AddClassificationsRequestValidator _subject = new();

    [Test]
    public async Task ValidateAsync_ValidationSuccess()
    {
        // Arrange
        var testModel = new AddClassifications()
        {
            Tag = "Subscription"
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsTrue();
            await Assert.That(result.Errors).Count().IsEqualTo(0);
        }
    }

    [Test]
    public async Task ValidateAsync_ContainsError_ValidationError()
    {
        // Arrange
        var testModel = new AddClassifications()
        {
            Tag = string.Empty
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(1);
        }
    }
}
