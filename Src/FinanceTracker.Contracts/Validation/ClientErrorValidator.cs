using FinanceTracker.Contracts.Diagnostics;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires a message, what logged it and the page, each within its limit, so one report can't fill the logs.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<ClientError>>]
public class ClientErrorValidator : AbstractValidator<ClientError>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public ClientErrorValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(ClientError.MaxMessageLength);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(ClientError.MaxNameLength);
        RuleFor(x => x.Page).NotEmpty().MaximumLength(ClientError.MaxNameLength);
        RuleFor(x => x.ExceptionType).MaximumLength(ClientError.MaxNameLength);
        RuleFor(x => x.StackTrace).MaximumLength(ClientError.MaxStackTraceLength);
    }
}
