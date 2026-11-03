using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FluentValidation;

namespace FinanceTracker.Contracts.Validation;

/// <summary>
/// Requires two to <see cref="SplitTransaction.MaxParts"/> parts, none of them zero, with short notes. Whether they
/// add up depends on the transaction, which the service checks.
/// </summary>
[InjectionCategory(InjectionCategoryType.Validator, ServiceDestinationType.Server | ServiceDestinationType.Client)]
[Scoped<IValidator<SplitTransaction>>]
public class SplitTransactionValidator : AbstractValidator<SplitTransaction>
{
    /// <summary>
    /// Sets up the rules.
    /// </summary>
    public SplitTransactionValidator()
    {
        RuleFor(x => x.Parts).Must(static x => x.Count >= 2).WithMessage("Split it into at least two parts");

        RuleFor(x => x.Parts).Must(static x => x.Count <= SplitTransaction.MaxParts)
            .WithMessage($"Split it into at most {SplitTransaction.MaxParts} parts");

        RuleForEach(x => x.Parts).ChildRules(static part =>
        {
            part.RuleFor(x => x.Amount).NotEqual(0).WithMessage("Enter an amount");

            part.RuleFor(x => x.Note).MaximumLength(SplitTransaction.MaxNoteLength)
                .WithMessage($"Keep the note to {SplitTransaction.MaxNoteLength} characters");
        });
    }
}
