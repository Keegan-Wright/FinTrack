using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a whole http or https address.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<SetWebhook>>]
public class SetWebhookValidator : AbstractValidator<SetWebhook>
{
    /// <summary>
    /// The longest address taken.
    /// </summary>
    public const int MaxLength = 2000;

    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public SetWebhookValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Enter the webhook's address")
            .MaximumLength(MaxLength).WithMessage($"Keep the address under {MaxLength} characters")
            .Must(BeHttp).WithMessage("Enter a whole address starting https:// or http://");
    }

    private static bool BeHttp(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? parsed)
        && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrEmpty(parsed.Host);
}
