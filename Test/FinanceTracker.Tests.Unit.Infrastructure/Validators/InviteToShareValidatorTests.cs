using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Validation;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class InviteToShareValidatorTests
{
    private readonly InviteToShareValidator _validator = new();

    [Test]
    [Arguments("sam")]
    [Arguments("sam@example.test")]
    public async Task Validate_AUsernameOrEmail_Passes(string who) =>
        await Assert.That(_validator.Validate(new InviteToShare { Who = who }).IsValid).IsTrue();

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Validate_Blank_AsksForThem(string who)
    {
        ValidationResult result = _validator.Validate(new InviteToShare { Who = who });

        await Assert.That(result.Errors.Select(static x => x.ErrorMessage)).Contains("Enter their username or email");
    }

    [Test]
    public async Task Validate_TooLong_Fails() =>
        await Assert.That(_validator.Validate(new InviteToShare { Who = new string('a', 257) }).IsValid).IsFalse();
}
