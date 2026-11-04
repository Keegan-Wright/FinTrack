using FinanceTracker.Contracts.Automation;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;
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
            Function = "SyncAllOpenBankingDetailsAsync",
            Description = "Description",
            Expression = "0 0 */4 * * ?",
            RetryIntervals =
            [
                10,
                20,
                30
            ],
            IsEnabled = true
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

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
            Function = "SyncAllOpenBankingDetailsAsync",
            Description = "Description",
            Expression = "0 0 */4 * * ?",
            RetryIntervals = null!,
            IsEnabled = true
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments("0 0 */4 * * *", "no ? in either day field")]
    [Arguments("0 0 */4 ? * ?", "? in both day fields")]
    [Arguments("0 */4 * * ?", "5 parts")]
    public async Task ValidateAsync_NotAQuartzCron_IsAnError(string expression, string because)
    {
        // Arrange
        var testModel = new UpdateCronJob
        {
            Function = "SyncAllOpenBankingDetailsAsync",
            Description = "Description",
            Expression = expression,
            RetryIntervals = [],
            IsEnabled = true
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Any(x => x.PropertyName == nameof(UpdateCronJob.Expression))).IsTrue()
            .Because(because);
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
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(4);

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(UpdateCronJob.Function))
                    .ErrorMessage).IsEqualTo("The job is required");

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(UpdateCronJob.Description))
                    .ErrorMessage).IsEqualTo("Description is required");

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(UpdateCronJob.Expression))
                    .ErrorMessage).StartsWith("Expression must be a Quartz cron");

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(UpdateCronJob.RetryIntervals))
                    .ErrorMessage).IsEqualTo("All intervals must be valid non-negative integers");
        }
    }
}
