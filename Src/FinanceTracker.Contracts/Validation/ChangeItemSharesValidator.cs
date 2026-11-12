using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires the list, each item in it once, of a kind and with a mode that exist.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<ChangeItemShares>>]
public class ChangeItemSharesValidator : AbstractValidator<ChangeItemShares>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public ChangeItemSharesValidator()
    {
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Say what to share, even if nothing")
            .Must(static x => x is null || x.Select(static i => (i.Kind, i.ItemId)).Distinct().Count() == x.Count)
            .WithMessage("Each item can only be shared once");

        RuleForEach(x => x.Items).ChildRules(static item =>
        {
            item.RuleFor(static x => x.Kind).IsInEnum().WithMessage("Choose what kind of item it is");
            item.RuleFor(static x => x.ItemId).NotEmpty().WithMessage("Choose an item");
            item.RuleFor(static x => x.Mode).IsInEnum().WithMessage("Choose what they can do with it");
        });
    }
}
