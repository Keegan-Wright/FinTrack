using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class RuleConditionValidatorTests : UnitTestBase
{
    private readonly RuleConditionValidator _subject = new();

    [Test]
    [Arguments(RuleField.Description, RuleOperator.Contains, "GROCER", null)]
    [Arguments(RuleField.Counterparty, RuleOperator.Regex, "^GAME (STORE|SHOP)$", null)]
    [Arguments(RuleField.Amount, RuleOperator.GreaterThan, "12.50", null)]
    [Arguments(RuleField.Amount, RuleOperator.Between, "5", "50.00")]
    [Arguments(RuleField.Type, RuleOperator.Equals, "debit", null)]
    [Arguments(RuleField.Account, RuleOperator.Equals, "0192f3a4-0000-7000-8000-000000000001", null)]
    public async Task ValidateAsync_SuitableOperatorAndValues_IsValid(RuleField field, RuleOperator @operator,
        string value, string? secondValue)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Condition(field, @operator, value, secondValue),
            CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments(RuleField.Description, RuleOperator.GreaterThan)]
    [Arguments(RuleField.Amount, RuleOperator.Contains)]
    [Arguments(RuleField.Type, RuleOperator.Contains)]
    [Arguments(RuleField.Account, RuleOperator.StartsWith)]
    public async Task ValidateAsync_OperatorDoesntSuitTheField_IsInvalid(RuleField field, RuleOperator @operator)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Condition(field, @operator, "1"), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).Contains(nameof(RuleCondition.Operator));
    }

    [Test]
    [Arguments(RuleField.Description, RuleOperator.Contains, "  ")]
    [Arguments(RuleField.Description, RuleOperator.Regex, "(unclosed")]
    [Arguments(RuleField.Amount, RuleOperator.Equals, "£12.50")]
    [Arguments(RuleField.Amount, RuleOperator.Equals, "12,50")]
    [Arguments(RuleField.Amount, RuleOperator.Equals, "1,250.00")]
    [Arguments(RuleField.Type, RuleOperator.Equals, "1")]
    [Arguments(RuleField.Account, RuleOperator.Equals, "main")]
    public async Task ValidateAsync_ValueDoesntSuitTheField_IsInvalid(RuleField field, RuleOperator @operator,
        string value)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Condition(field, @operator, value), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).Contains(nameof(RuleCondition.Value));
    }

    [Test]
    [Arguments(RuleOperator.Between, "50", "5")]
    [Arguments(RuleOperator.Between, "5", null)]
    [Arguments(RuleOperator.Equals, "5", "50")]
    public async Task ValidateAsync_SecondValueOutOfPlace_IsInvalid(RuleOperator @operator, string value,
        string? secondValue)
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(
            Condition(RuleField.Amount, @operator, value, secondValue), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .Contains(nameof(RuleCondition.SecondValue));
    }

    private static RuleCondition Condition(RuleField field, RuleOperator @operator, string value,
        string? secondValue = null) =>
        new() { Field = field, Operator = @operator, Value = value, SecondValue = secondValue };
}
