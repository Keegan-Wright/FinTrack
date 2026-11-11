using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires the username or email address of who to invite.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<InviteToShare>>]
public class InviteToShareValidator : AbstractValidator<InviteToShare>
{
    /// <summary>
    /// The longest username or email address Identity stores.
    /// </summary>
    public const int MaxLength = 256;

    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public InviteToShareValidator()
    {
        RuleFor(x => x.Who)
            .NotEmpty().WithMessage("Enter their username or email")
            .MaximumLength(MaxLength).WithMessage($"Usernames and emails are at most {MaxLength} characters");
    }
}
