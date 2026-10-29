using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class ReorderRulesValidatorTests : UnitTestBase
{
    private readonly ReorderRulesValidator _subject = new();

    [Test]
    public async Task ValidateAsync_EachRuleOnce_IsValid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(
            new ReorderRules { RuleIds = [Guid.CreateVersion7(), Guid.CreateVersion7()] }, CancellationToken);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task ValidateAsync_NoRulesOrARuleTwice_IsInvalid()
    {
        // Arrange
        Guid ruleId = Guid.CreateVersion7();

        // Act
        ValidationResult empty = await _subject.ValidateAsync(new ReorderRules(), CancellationToken);
        ValidationResult twice = await _subject.ValidateAsync(new ReorderRules { RuleIds = [ruleId, ruleId] },
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(empty.IsValid).IsFalse();
            await Assert.That(twice.IsValid).IsFalse();
        }
    }
}
