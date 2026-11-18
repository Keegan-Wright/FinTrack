using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Validation;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class MergeJointAccountsValidatorTests
{
    private static readonly Guid YourAccountId = Guid.NewGuid();
    private static readonly Guid TheirAccountId = Guid.NewGuid();
    private readonly MergeJointAccountsValidator _validator = new();

    [Test]
    public async Task Validate_SeveralPairs_Passes()
    {
        // Arrange
        MergeJointAccounts request = new()
        {
            Accounts = [Pair(YourAccountId, TheirAccountId), Pair(Guid.NewGuid(), Guid.NewGuid())]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_NoneChosen_Fails()
    {
        // Act
        ValidationResult result = _validator.Validate(new MergeJointAccounts { Accounts = [] });

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage)).Contains("Choose an account to merge");
    }

    [Test]
    public async Task Validate_YourAccountInTwoPairs_Fails()
    {
        // Arrange
        MergeJointAccounts request = new()
        {
            Accounts = [Pair(YourAccountId, TheirAccountId), Pair(YourAccountId, Guid.NewGuid())]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .Contains("Each account can only be merged once");
    }

    [Test]
    public async Task Validate_AnAccountMergedWithItself_Fails()
    {
        // Arrange
        MergeJointAccounts request = new() { Accounts = [Pair(YourAccountId, YourAccountId)] };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .Contains("Each account can only be merged once");
    }

    [Test]
    public async Task Validate_APairMissingACopy_FailsWithThePairsMessage()
    {
        // Arrange
        MergeJointAccounts request = new() { Accounts = [Pair(YourAccountId, Guid.Empty)] };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .Contains("Choose their copy of the account");
    }

    private static MergeJointAccount Pair(Guid yourAccountId, Guid theirAccountId) =>
        new() { YourAccountId = yourAccountId, TheirAccountId = theirAccountId };
}
