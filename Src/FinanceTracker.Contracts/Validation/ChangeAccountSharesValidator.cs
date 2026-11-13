using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires the list, each account in it once, with a mode that exists.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<ChangeAccountShares>>]
public class ChangeAccountSharesValidator : AbstractValidator<ChangeAccountShares>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public ChangeAccountSharesValidator()
    {
        RuleFor(x => x.Accounts)
            .NotNull().WithMessage("Say which accounts to share, even if none")
            .Must(static x => x is null || x.Select(static a => a.AccountId).Distinct().Count() == x.Count)
            .WithMessage("Each account can only be shared once");

        RuleForEach(x => x.Accounts).ChildRules(static account =>
        {
            account.RuleFor(static x => x.AccountId).NotEmpty().WithMessage("Choose an account");
            account.RuleFor(static x => x.Mode).IsInEnum().WithMessage("Choose what they can do with it");
        });
    }
}
