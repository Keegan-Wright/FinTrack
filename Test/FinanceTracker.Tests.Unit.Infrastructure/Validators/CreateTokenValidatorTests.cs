using FinanceTracker.Contracts.Tokens;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class CreateTokenValidatorTests : UnitTestBase
{
    private readonly CreateTokenValidator _subject = new();

    [Test]
    public async Task ValidateAsync_NamedWithAPassword_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(
            new CreateToken { Name = "Home Assistant", Expiry = TokenExpiry.Never, Password = "Placeholder" },
            CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_NoName_SaysToGiveItOne()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new CreateToken(), CancellationToken);

        // Assert
        // The password isn't checked here, as only a password user gives one
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage)).IsEquivalentTo(["Give it a name"]);
    }

    [Test]
    public async Task ValidateAsync_NameTooLongOrUnknownExpiry_IsInvalid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(
            new CreateToken
            {
                Name = new string('a', TokenNames.MaximumLength + 1), Expiry = (TokenExpiry)99, Password = "Placeholder"
            },
            CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .IsEquivalentTo(["Use no more than 64 characters", "Choose how long it lasts"]);
    }
}
