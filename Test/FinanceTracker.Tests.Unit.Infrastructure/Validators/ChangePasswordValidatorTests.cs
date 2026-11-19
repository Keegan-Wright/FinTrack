using FinanceTracker.Contracts.Auth;
using FinanceTracker.Contracts.Profile;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class ChangePasswordValidatorTests : UnitTestBase
{
    private readonly ChangePasswordValidator _subject = new();

    [Test]
    public async Task ValidateAsync_LongEnoughAndMatching_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Change("Placeholder-Password"), CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments(PasswordRules.MinimumLength - 1, PasswordRules.TooShort)]
    [Arguments(PasswordRules.MaximumLength + 1, PasswordRules.TooLong)]
    public async Task ValidateAsync_NewPasswordOutsideTheRules_SaysWhy(int length, string expected)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Change(new string('a', length)), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Single().ErrorMessage).IsEqualTo(expected);
    }

    [Test]
    public async Task ValidateAsync_NewPasswordEmpty_SaysOnlyToEnterOne()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Change(string.Empty), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Single().ErrorMessage).IsEqualTo("Enter a new password");
    }

    /// <summary>
    /// A change to <paramref name="newPassword"/>, typed the same twice.
    /// </summary>
    private static ChangePassword Change(string newPassword) => new()
    {
        CurrentPassword = "Current-Placeholder", NewPassword = newPassword, ConfirmNewPassword = newPassword
    };
}
