using FinanceTracker.Contracts.Exports;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class ExportBackupValidatorTests : UnitTestBase
{
    private readonly ExportBackupValidator _subject = new();

    [Test]
    public async Task ValidateAsync_NoPassword_IsValid()
    {
        // Act, a plain backup
        ValidationResult result = await _subject.ValidateAsync(new ExportBackup(), CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_LongEnoughAndMatching_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Protected(new string('a', 16)), CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments(BackupPasswordRules.MinimumLength - 1, BackupPasswordRules.TooShort)]
    [Arguments(BackupPasswordRules.MaximumLength + 1, BackupPasswordRules.TooLong)]
    public async Task ValidateAsync_PasswordOutsideTheRules_SaysWhy(int length, string expected)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Protected(new string('a', length)), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Single().ErrorMessage).IsEqualTo(expected);
    }

    [Test]
    public async Task ValidateAsync_PasswordEmpty_SaysOnlyToEnterOne()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Protected(string.Empty), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Single().ErrorMessage).IsEqualTo("Enter a password");
    }

    [Test]
    public async Task ValidateAsync_ConfirmDiffers_SaysTheyDontMatch()
    {
        // Arrange
        ExportBackup backup = new()
        {
            Password = "Placeholder backup password", ConfirmPassword = "Placeholder backup passw0rd"
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(backup, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Single().ErrorMessage).IsEqualTo("The passwords don't match");
    }

    /// <summary>
    /// A protected backup with <paramref name="password"/>, typed the same twice.
    /// </summary>
    private static ExportBackup Protected(string password) => new()
    {
        Password = password, ConfirmPassword = password
    };
}
