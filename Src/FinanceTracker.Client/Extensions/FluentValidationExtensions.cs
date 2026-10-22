using FluentValidation;
using FluentValidation.Results;

namespace FinanceTracker.Client.Extensions;

/// <summary>
/// Bridges FluentValidation validators (from Contracts) to MudBlazor's per field validation.
/// </summary>
public static class FluentValidationExtensions
{
    extension<T>(IValidator<T> validator)
    {
        /// <summary>
        /// A MudBlazor <c>Validation</c> function that checks only <paramref name="propertyName"/> of
        /// <paramref name="model"/>, so a field shows its own errors as the user types.
        /// </summary>
        /// <param name="model">The form's model, validated as it is when the field changes.</param>
        /// <param name="propertyName">The property the field is bound to, use <c>nameof</c>.</param>
        public Func<object, string, Task<IEnumerable<string>>> ValidateValue(T model, string propertyName)
        {
            return async (obj, property) =>
            {
                ValidationResult result = await validator.ValidateAsync(ValidationContext<T>.CreateWithOptions(model, x => x.IncludeProperties(propertyName)));
                if (result.IsValid)
                    return Array.Empty<string>();
                return result.Errors.Select(e => e.ErrorMessage);
            };
        }
    }
}
