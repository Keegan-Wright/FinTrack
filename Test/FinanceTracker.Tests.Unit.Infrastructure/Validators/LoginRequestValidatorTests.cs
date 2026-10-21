using FinanceTracker.Contracts.Auth;
using FinanceTracker.Infrastructure.Validation;
using FinanceTracker.Tests.Unit.Shared;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class LoginRequestValidatorTests : UnitTestBase
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
        var result = await _subject.ValidateAsync(testModel, CancellationToken);

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
        var result = await _subject.ValidateAsync(testModel, CancellationToken);

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
