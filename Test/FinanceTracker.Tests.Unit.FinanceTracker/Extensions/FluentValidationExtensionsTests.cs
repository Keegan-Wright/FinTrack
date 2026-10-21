using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Extensions;
using FinanceTracker.Infrastructure.Validation;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

public class FluentValidationExtensionsTests
{
    private readonly AddHouseholdMemberRequestValidator _validator = new();

    [Test]
    public async Task ValidateValue_InvalidProperty_ReturnsItsErrors()
    {
        // Arrange
        var model = new AddHouseholdMember { FirstName = "", LastName = "Doe", Income = 0 };

        // Act
        IEnumerable<string> errors = await _validator.ValidateValue(model, nameof(AddHouseholdMember.FirstName))
            (model, nameof(AddHouseholdMember.FirstName));

        // Assert
        await Assert.That(errors).IsEquivalentTo(["First Name is required"]);
    }

    [Test]
    public async Task ValidateValue_ValidProperty_ReturnsNoErrors()
    {
        // Arrange
        var model = new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", Income = 0 };

        // Act
        IEnumerable<string> errors = await _validator.ValidateValue(model, nameof(AddHouseholdMember.FirstName))
            (model, nameof(AddHouseholdMember.FirstName));

        // Assert
        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task ValidateValue_OnlyValidatesTheRequestedProperty()
    {
        // Arrange
        var model = new AddHouseholdMember { FirstName = "Jane", LastName = "", Income = -1 };

        // Act
        IEnumerable<string> errors = await _validator.ValidateValue(model, nameof(AddHouseholdMember.FirstName))
            (model, nameof(AddHouseholdMember.FirstName));

        // Assert
        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task ValidateValue_ValidatesModelCapturedWhenCreated()
    {
        // Arrange
        var model = new AddHouseholdMember { FirstName = "Jane", LastName = "Doe", Income = -1 };
        Func<object, string, Task<IEnumerable<string>>> validate = _validator.ValidateValue(model, nameof(AddHouseholdMember.Income));

        // Act
        model.Income = 10;
        IEnumerable<string> errors = await validate(new object(), nameof(AddHouseholdMember.Income));

        // Assert
        await Assert.That(errors).IsEmpty();
    }
}
