using FinanceTracker.Models.Request.Classifications;
using FinanceTracker.Tests.Shared;
using FinanceTracker.Validators.Models;

namespace FinanceTracker.Tests.Validators.Models;

public class AddClassificationsRequestValidatorTests : TestFixtureBase
{
    private readonly AddClassificationsRequestValidator _subject = new();

    [Test]
    public async Task ValidateAsync_ValidationSuccess()
    {
        // Arrange
        var testModel = new AddClassificationsRequest()
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
        var testModel = new AddClassificationsRequest()
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
