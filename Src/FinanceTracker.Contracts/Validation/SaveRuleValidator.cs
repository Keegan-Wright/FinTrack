using FinanceTracker.Contracts.Rules;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a name, at least one valid condition, see <see cref="RuleConditionValidator"/>, and at least one action
/// a tag for each add tag action, a bank classification for each hide action, a bill for each pay bill action, at most
/// one action that decides the transfer answer and at most one that pays a bill.
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

        RuleFor(x => x.Actions)
            .Must(x => x.Count(static action => action.Type == RuleActionType.PayBill) <= 1)
            .WithMessage("A rule can only pay one bill");

        RuleFor(x => x.Actions)
            .Must(x => x.Count(static action => action.Type == RuleActionType.PayDebt) <= 1)
            .WithMessage("A rule can only pay off one debt");

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

            action.RuleFor(x => x.BankClassification)
                .NotEmpty()
                .When(x => x.Type == RuleActionType.HideClassification)
                .WithMessage("Enter the bank classification to hide");

            action.RuleFor(x => x.BankClassification)
                .Empty()
                .When(x => x.Type != RuleActionType.HideClassification)
                .WithMessage("Only hiding takes a bank classification");

            action.RuleFor(x => x.BillId)
                .NotNull()
                .When(x => x.Type == RuleActionType.PayBill)
                .WithMessage("Choose a bill");

            action.RuleFor(x => x.BillId)
                .Null()
                .When(x => x.Type != RuleActionType.PayBill)
                .WithMessage("Only pay bill takes a bill");

            action.RuleFor(x => x.DebtId)
                .NotNull()
                .When(x => x.Type == RuleActionType.PayDebt)
                .WithMessage("Choose a debt");

            action.RuleFor(x => x.DebtId)
                .Null()
                .When(x => x.Type != RuleActionType.PayDebt)
                .WithMessage("Only pay debt takes a debt");
        });
    }

    private static bool DecidesTransfer(RuleActionType type) =>
        type is RuleActionType.MarkAsTransfer or RuleActionType.MarkNotATransfer;
}
