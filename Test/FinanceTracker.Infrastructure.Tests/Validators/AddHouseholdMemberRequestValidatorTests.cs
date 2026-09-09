using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Infrastructure.Validation;
using FinanceTracker.Tests.Shared;

namespace FinanceTracker.Infrastructure.Tests.Validators;

public class AddHouseholdMemberRequestValidatorTests : TestFixtureBase
{
    private readonly AddHouseholdMemberRequestValidator _subject = new();

    [Test]
    public async Task ValidateAsync_ValidationSuccess()
    {
        // Arrange
        var testModel = new AddHouseholdMember()
        {
            FirstName = "Test",
            LastName = "User",
            Income = 100M
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
    public async Task ValidateAsync_ZeroIncome_ValidationSuccess()
    {
        // Arrange
        var testModel = new AddHouseholdMember()
        {
            FirstName = "Test",
            LastName = "User",
            Income = 0M
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
        var testModel = new AddHouseholdMember()
        {
            FirstName = string.Empty,
            LastName = string.Empty,
            Income = -1M
        };

        // Act
        var result = await _subject.ValidateAsync(testModel, _cancellationTokenSource.Token);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors).Count().IsEqualTo(3);

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(AddHouseholdMember.FirstName))
                    .ErrorMessage).IsEqualTo("First Name is required");

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(AddHouseholdMember.LastName))
                    .ErrorMessage).IsEqualTo("Last Name is required");

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(AddHouseholdMember.Income))
                    .ErrorMessage).IsEqualTo("Income must be 0 or greater");
        }
    }
}
