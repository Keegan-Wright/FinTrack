using FinanceTracker.Contracts.Rules;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a name, at least one valid condition, see <see cref="RuleConditionValidator"/>, and at least one action
/// a tag for each add tag action and at most one action that decides the transfer answer.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<SaveRule>>]
public class SaveRuleValidator : AbstractValidator<SaveRule>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public SaveRuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");

        RuleFor(x => x.MatchMode).IsInEnum().WithMessage("Choose all or any");

        RuleFor(x => x.Conditions).NotEmpty().WithMessage("Add at least one condition");
        RuleForEach(x => x.Conditions).SetValidator(new RuleConditionValidator());

        RuleFor(x => x.Actions).NotEmpty().WithMessage("Add at least one action");

        RuleFor(x => x.Actions)
            .Must(x => x.Count(static action => DecidesTransfer(action.Type)) <= 1)
            .WithMessage("Only one action can decide whether it's a transfer");

        RuleForEach(x => x.Actions).ChildRules(action =>
        {
            action.RuleFor(x => x.Type).IsInEnum().WithMessage("Choose what the action does");

            action.RuleFor(x => x.ClassificationId)
                .NotNull()
                .When(x => x.Type == RuleActionType.AddTag)
                .WithMessage("Choose a tag");

            action.RuleFor(x => x.ClassificationId)
                .Null()
                .When(x => x.Type != RuleActionType.AddTag)
                .WithMessage("Only add tag takes a tag");
        });
    }

    private static bool DecidesTransfer(RuleActionType type) =>
        type is RuleActionType.MarkAsTransfer or RuleActionType.MarkNotATransfer;
}
