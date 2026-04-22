using FinanceTracker.Models.Request.HouseholdMember;
using FinanceTracker.Tests.Shared;
using FinanceTracker.Validators.Models;

namespace FinanceTracker.Tests.Validators.Models;

public class AddHouseholdMemberRequestValidatorTests : TestFixtureBase
{
    private readonly AddHouseholdMemberRequestValidator _subject = new();

    [Test]
    public async Task ValidateAsync_ValidationSuccess()
    {
        // Arrange
        var testModel = new AddHouseholdMemberRequest()
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
    public async Task ValidateAsync_ContainsError_ValidationError()
    {
        // Arrange
        var testModel = new AddHouseholdMemberRequest()
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
                .That(result.Errors.First(x => x.PropertyName == nameof(AddHouseholdMemberRequest.FirstName))
                    .ErrorMessage).IsEqualTo("First Name is required");

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(AddHouseholdMemberRequest.LastName))
                    .ErrorMessage).IsEqualTo("Last Name is required");

            await Assert
                .That(result.Errors.First(x => x.PropertyName == nameof(AddHouseholdMemberRequest.Income))
                    .ErrorMessage).IsEqualTo("Income must be 0 or greater");
        }
    }
}
