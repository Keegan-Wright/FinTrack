using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires at least one pair, each with both copies, and each account in one pair only.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<MergeJointAccounts>>]
public class MergeJointAccountsValidator : AbstractValidator<MergeJointAccounts>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public MergeJointAccountsValidator()
    {
        RuleFor(static x => x.Accounts)
            .NotEmpty().WithMessage("Choose an account to merge")
            .Must(static x => x is null || x.SelectMany(static a => new[] { a.YourAccountId, a.TheirAccountId })
                .Distinct().Count() == x.Count * 2)
            .WithMessage("Each account can only be merged once");

        RuleForEach(static x => x.Accounts).SetValidator(new MergeJointAccountValidator());
    }
}
