using FluentValidation;
using FluentValidation.Results;

namespace FinanceTracker.Extensions;

public static class FluentValidationExtensions
{
    extension<T>(IValidator<T> validator)
    {
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
