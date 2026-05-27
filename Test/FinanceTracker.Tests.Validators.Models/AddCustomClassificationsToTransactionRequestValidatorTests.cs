using FinanceTracker.Models.Request.Classifications;
using FinanceTracker.Tests.Shared;
using FinanceTracker.Validators.Models;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Validators.Models;

public class AddCustomClassificationsToTransactionRequestValidatorTests : TestFixtureBase
{
    private readonly AddCustomClassificationsToTransactionRequestValidator _subject = new();

    [Test]
    public async Task ValidateAsync_ValidationSuccess()
    {
        // Arrange
        var testModel = new AddCustomClassificationsToTransactionRequest
        {
            TransactionId = Guid.NewGuid(),
            Classifications = new []
            {
                new SelectedCustomClassificationsRequest()
                {
                    ClassificationId = Guid.NewGuid()
                }
            }
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
    public async Task ValidateAsync_EmptyGuidTransactionId_ValidationError()
    {
        // Arrange
        var testModel = new AddCustomClassificationsToTransactionRequest
        {
            TransactionId = Guid.Empty,
            Classifications = new []
            {
                new SelectedCustomClassificationsRequest()
                {
                    ClassificationId = Guid.NewGuid()
                }
            }
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(1);
            await Assert.That(result.Errors.First().PropertyName).IsEqualTo(nameof(AddCustomClassificationsToTransactionRequest.TransactionId));
            await Assert.That(result.Errors.First().ErrorMessage).IsEqualTo("Invalid transaction id");
        }
    }

    [Test]
    public async Task ValidateAsync_EmptyClassifications_ValidationError()
    {
        // Arrange
        var testModel = new AddCustomClassificationsToTransactionRequest
        {
            TransactionId = Guid.NewGuid(),
            Classifications = []
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(1);
            await Assert.That(result.Errors.First().PropertyName).IsEqualTo(nameof(AddCustomClassificationsToTransactionRequest.Classifications));
            await Assert.That(result.Errors.First().ErrorMessage).IsEqualTo("At least one classification must be selected");
        }
    }

    [Test]
    public async Task ValidateAsync_ClassificationsEmptyGuid_ValidationError()
    {
        // Arrange
        var testModel = new AddCustomClassificationsToTransactionRequest
        {
            TransactionId = Guid.NewGuid(),
            Classifications = new []
            {
                new SelectedCustomClassificationsRequest()
                {
                    ClassificationId = Guid.Empty
                },
                new SelectedCustomClassificationsRequest()
                {
                    ClassificationId = Guid.Empty
                },
                new SelectedCustomClassificationsRequest()
                {
                    ClassificationId = Guid.Empty
                },
            }
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(3);
            await Assert.That(result.Errors).All(x => x.PropertyName.EndsWith(nameof(SelectedCustomClassificationsRequest.ClassificationId)));
            foreach (ValidationFailure validationFailure in result.Errors)
            {
                await Assert.That(validationFailure.ErrorMessage)
                    .IsEqualTo($"Classification {result.Errors.IndexOf(validationFailure)} is invalid");
            }
        }
    }
}
