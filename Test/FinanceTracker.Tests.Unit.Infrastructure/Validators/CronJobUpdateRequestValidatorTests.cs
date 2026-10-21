using FinanceTracker.Contracts.Automation;
using FinanceTracker.Infrastructure.Validation;
using FinanceTracker.Tests.Unit.Shared;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class CronJobUpdateRequestValidatorTests : UnitTestBase
{
    private readonly CronJobUpdateRequestValidator _subject = new();

    [Test]
    public async Task ValidateAsync_ValidationSuccess()
    {
        // Arrange
        var testModel = new UpdateCronJob
        {
            Description = "Description",
            Expression = "* * * * * *",
            RetryIntervals =
            [
                10,
                20,
                30
            ],
            IsEnabled = true
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsTrue();
            await Assert.That(result.Errors).Count().IsEqualTo(0);
        }
    }

    [Test]
    public async Task ValidateAsync_NoRetryIntervals_ValidationSuccess()
    {
        // Arrange
        var testModel = new UpdateCronJob
        {
            Description = "Description",
            Expression = "* * * * * *",
            RetryIntervals = null!,
            IsEnabled = true
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_ContainsError_ValidationError()
    {
        // Arrange
        var testModel = new UpdateCronJob()
        {
            Description = string.Empty,
            Expression = string.Empty,
            RetryIntervals = [10, -20],
            IsEnabled = true
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(3);

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(UpdateCronJob.Description))
                    .ErrorMessage).IsEqualTo("Description is required");

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(UpdateCronJob.Expression))
                    .ErrorMessage).IsEqualTo("Expression must be a 6-part cron");

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(UpdateCronJob.RetryIntervals))
                    .ErrorMessage).IsEqualTo("All intervals must be valid non-negative integers");
        }
    }
}
