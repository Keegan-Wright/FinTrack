using FinanceTracker.Contracts.Auth;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class RegisterRequestValidatorTests : UnitTestBase
{
    private readonly RegisterRequestValidator _subject = new();

    [Test]
    public async Task ValidateAsync_ValidationSuccess()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = "Placeholder-Password",
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = "Placeholder-Password"
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
    public async Task ValidateAsync_UsernameEmpty_ValidationError()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = string.Empty,
            Password = "Placeholder-Password",
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = "Placeholder-Password"
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(1);
            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(RegisterRequest.Username))
                    .ErrorMessage).IsEqualTo("Username is required");
        }
    }

    [Test]
    public async Task ValidateAsync_PasswordEmpty_ValidationError()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = string.Empty,
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = string.Empty
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(2);
            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(RegisterRequest.Password))
                    .ErrorMessage).IsEqualTo("Password is required");
        }
    }

    [Test]
    public async Task ValidateAsync_EmailEmpty_ValidationError()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = "Placeholder-Password",
            Email = string.Empty,
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = "Placeholder-Password"
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(2);
            await Assert
                .That(result.Errors.First(x =>
                        x.PropertyName == nameof(RegisterRequest.Email) && x.ErrorMessage == "Email is required")
                    .ErrorMessage).IsEqualTo("Email is required");
        }
    }

    [Test]
    public async Task ValidateAsync_EmailInvalid_ValidationError()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = "Placeholder-Password",
            Email = "invalid-email",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = "Placeholder-Password"
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(1);
            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(RegisterRequest.Email))
                    .ErrorMessage).IsEqualTo("Invalid email address");
        }
    }

    [Test]
    public async Task ValidateAsync_FirstNameEmpty_ValidationError()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = "Placeholder-Password",
            Email = "Email@Address.com",
            FirstName = string.Empty,
            LastName = "Last name",
            ConfirmPassword = "Placeholder-Password"
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(1);
            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(RegisterRequest.FirstName))
                    .ErrorMessage).IsEqualTo("First name is required");
        }
    }

    [Test]
    public async Task ValidateAsync_LastNameEmpty_ValidationError()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = "Placeholder-Password",
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = string.Empty,
            ConfirmPassword = "Placeholder-Password"
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(1);
            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(RegisterRequest.LastName))
                    .ErrorMessage).IsEqualTo("Last name is required");
        }
    }

    [Test]
    public async Task ValidateAsync_ConfirmPasswordEmpty_ValidationError()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = "Placeholder-Password",
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = string.Empty
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(2);
            await Assert
                .That(result.Errors.First(x =>
                        x.PropertyName == nameof(RegisterRequest.ConfirmPassword) &&
                        x.ErrorMessage == "Confirm password is required")
                    .ErrorMessage).IsEqualTo("Confirm password is required");
        }
    }

    [Test]
    public async Task ValidateAsync_ConfirmPasswordMismatch_ValidationError()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = "Placeholder-Password",
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = "WrongPassword"
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(1);
            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(RegisterRequest.ConfirmPassword))
                    .ErrorMessage).IsEqualTo("Passwords do not match");
        }
    }

    [Test]
    public async Task ValidateAsync_ContainsErrors_ValidationError()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = string.Empty,
            Password = string.Empty,
            Email = string.Empty,
            FirstName = string.Empty,
            LastName = string.Empty,
            ConfirmPassword = string.Empty
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(7);
        }
    }

    [Test]
    [Arguments(PasswordRules.MinimumLength - 1, PasswordRules.TooShort)]
    [Arguments(PasswordRules.MaximumLength + 1, PasswordRules.TooLong)]
    public async Task ValidateAsync_PasswordOutsideTheRules_SaysWhy(int length, string expected)
    {
        // Arrange
        string password = new('a', length);
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = password,
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = password
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors.Single().ErrorMessage).IsEqualTo(expected);
        }
    }

    [Test]
    [Arguments(PasswordRules.MinimumLength)]
    [Arguments(PasswordRules.MaximumLength)]
    public async Task ValidateAsync_PasswordAtTheLimits_IsValid(int length)
    {
        // Arrange
        string password = new('a', length);
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = password,
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = password
        };

        // Act
        ValidationResult result = await _subject.ValidateAsync(testModel, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }
}
