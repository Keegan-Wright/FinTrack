using FinanceTracker.Contracts.Auth;
using FinanceTracker.Infrastructure.Validation;
using FinanceTracker.Tests.Shared;

namespace FinanceTracker.Infrastructure.Tests.Validators;

public class LoginRequestValidatorTests : TestFixtureBase
{
    private readonly LoginRequestValidator _subject = new();

    [Test]
    public async Task ValidateAsync_ValidationSuccess()
    {
        // Arrange
        var testModel = new LoginRequest()
        {
            Username = "Username",
            Password = "Password"
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
        var testModel = new LoginRequest()
        {
            Username = string.Empty,
            Password = string.Empty
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(2);

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(LoginRequest.Username))
                    .ErrorMessage).IsEqualTo("Username is required");

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(LoginRequest.Password))
                    .ErrorMessage).IsEqualTo("Password is required");
        }
    }
}
