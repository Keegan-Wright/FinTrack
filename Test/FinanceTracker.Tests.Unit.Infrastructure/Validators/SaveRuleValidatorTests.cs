using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class SaveRuleValidatorTests : UnitTestBase
{
    private static readonly Guid TagId = Guid.CreateVersion7();

    private readonly SaveRuleValidator _subject = new();

    [Test]
    public async Task ValidateAsync_NameConditionAndAction_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(Valid(), CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_NoNameConditionsOrActions_IsInvalid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new SaveRule(), CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).IsEquivalentTo(
            [nameof(SaveRule.Name), nameof(SaveRule.Conditions), nameof(SaveRule.Actions)]);
    }

    [Test]
    public async Task ValidateAsync_InvalidCondition_IsInvalid()
    {
        // Arrange
        SaveRule rule = Valid();
        rule.Conditions[0].Operator = RuleOperator.Between;

        // Act
        ValidationResult result = await _subject.ValidateAsync(rule, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task ValidateAsync_TwoTransferAnswers_IsInvalid()
    {
        // Arrange
        SaveRule rule = Valid();
        rule.Actions =
        [
            new RuleAction { Type = RuleActionType.MarkAsTransfer },
            new RuleAction { Type = RuleActionType.MarkNotATransfer }
        ];

        // Act
        ValidationResult result = await _subject.ValidateAsync(rule, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).Contains(nameof(SaveRule.Actions));
    }

    [Test]
    [Arguments(RuleActionType.AddTag, false)]
    [Arguments(RuleActionType.MarkAsTransfer, true)]
    public async Task ValidateAsync_TagOnTheWrongAction_IsInvalid(RuleActionType type, bool withTag)
    {
        // Arrange
        SaveRule rule = Valid();
        rule.Actions = [new RuleAction { Type = type, ClassificationId = withTag ? TagId : null }];

        // Act
        ValidationResult result = await _subject.ValidateAsync(rule, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    [Arguments(RuleActionType.HideClassification, null)]
    [Arguments(RuleActionType.MarkAsTransfer, "TV")]
    public async Task ValidateAsync_BankClassificationOnTheWrongAction_IsInvalid(RuleActionType type,
        string? bankClassification)
    {
        // Arrange
        SaveRule rule = Valid();
        rule.Actions = [new RuleAction { Type = type, BankClassification = bankClassification }];

        // Act
        ValidationResult result = await _subject.ValidateAsync(rule, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task ValidateAsync_HidingABankClassification_IsValid()
    {
        // Arrange
        SaveRule rule = Valid();
        rule.Actions = [new RuleAction { Type = RuleActionType.HideClassification, BankClassification = "TV" }];

        // Act
        ValidationResult result = await _subject.ValidateAsync(rule, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    private static SaveRule Valid() => new()
    {
        Name = "Groceries",
        Conditions = [new RuleCondition { Field = RuleField.Description, Operator = RuleOperator.Contains,
            Value = "GROCER" }],
        Actions = [new RuleAction { Type = RuleActionType.AddTag, ClassificationId = TagId }]
    };
}
