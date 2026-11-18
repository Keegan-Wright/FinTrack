using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Validation;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class ChangeItemSharesValidatorTests
{
    private static readonly Guid ItemId = Guid.NewGuid();
    private readonly ChangeItemSharesValidator _validator = new();

    [Test]
    public async Task Validate_NoneChosen_Passes()
    {
        // Act
        ValidationResult result = _validator.Validate(new ChangeItemShares { Items = [] });

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_SeveralItems_Passes()
    {
        // Arrange
        ChangeItemShares request = new()
        {
            Items =
            [
                Choice(SharedItemKind.Bill, Guid.NewGuid(), ShareMode.ReadOnly),
                Choice(SharedItemKind.Budget, Guid.NewGuid(), ShareMode.AllowChanges)
            ]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_NoList_Fails()
    {
        // Act
        ValidationResult result = _validator.Validate(new ChangeItemShares { Items = null! });

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .Contains("Say what to share, even if nothing");
    }

    [Test]
    public async Task Validate_AnItemTwice_Fails()
    {
        // Arrange
        ChangeItemShares request = new()
        {
            Items =
            [
                Choice(SharedItemKind.Bill, ItemId, ShareMode.ReadOnly),
                Choice(SharedItemKind.Bill, ItemId, ShareMode.AllowChanges)
            ]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage))
            .Contains("Each item can only be shared once");
    }

    [Test]
    public async Task Validate_TheSameIdForDifferentKinds_Passes()
    {
        // Arrange
        ChangeItemShares request = new()
        {
            Items =
            [
                Choice(SharedItemKind.Bill, ItemId, ShareMode.ReadOnly),
                Choice(SharedItemKind.Debt, ItemId, ShareMode.ReadOnly)
            ]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_AKindThatDoesntExist_Fails()
    {
        // Arrange
        ChangeItemShares request = new() { Items = [Choice((SharedItemKind)42, ItemId, ShareMode.ReadOnly)] };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage)).Contains("Choose what kind of item it is");
    }

    [Test]
    public async Task Validate_NoItemId_Fails()
    {
        // Arrange
        ChangeItemShares request = new() { Items = [Choice(SharedItemKind.Bill, Guid.Empty, ShareMode.ReadOnly)] };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage)).Contains("Choose an item");
    }

    [Test]
    public async Task Validate_AModeThatDoesntExist_Fails()
    {
        // Arrange
        ChangeItemShares request = new() { Items = [Choice(SharedItemKind.Bill, ItemId, (ShareMode)42)] };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.ErrorMessage)).Contains("Choose what they can do with it");
    }

    private static ItemShareChoice Choice(SharedItemKind kind, Guid itemId, ShareMode mode) =>
        new() { Kind = kind, ItemId = itemId, Mode = mode };
}
