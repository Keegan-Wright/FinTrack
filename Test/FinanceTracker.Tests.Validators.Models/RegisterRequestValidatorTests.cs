using FinanceTracker.Models.Request.Auth;
using FinanceTracker.Tests.Shared;
using FinanceTracker.Validators.Models;

namespace FinanceTracker.Tests.Validators.Models;

public class RegisterRequestValidatorTests : TestFixtureBase
{
    private readonly RegisterRequestValidator _subject = new();

    [Test]
    public async Task ValidateAsync_ValidationSuccess()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = "Username",
            Password = "Password",
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = "Password"
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
    public async Task ValidateAsync_UsernameEmpty_ValidationError()
    {
        // Arrange
        var testModel = new RegisterRequest()
        {
            Username = string.Empty,
            Password = "Password",
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = "Password"
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

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
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

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
            Password = "Password",
            Email = string.Empty,
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = "Password"
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(2);
            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(RegisterRequest.Email) && x.ErrorMessage == "Email is required")
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
            Password = "Password",
            Email = "invalid-email",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = "Password"
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

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
            Password = "Password",
            Email = "Email@Address.com",
            FirstName = string.Empty,
            LastName = "Last name",
            ConfirmPassword = "Password"
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

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
            Password = "Password",
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = string.Empty,
            ConfirmPassword = "Password"
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

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
            Password = "Password",
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = string.Empty
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(2);
            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(RegisterRequest.ConfirmPassword) && x.ErrorMessage == "Confirm password is required")
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
            Password = "Password",
            Email = "Email@Address.com",
            FirstName = "First name",
            LastName = "Last name",
            ConfirmPassword = "WrongPassword"
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

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
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(7);
        }
    }

}
