using FluentValidation;

namespace FinanceTracker.Extensions;

public static class FluentValidationExtensions
{
    public static Func<object, string, Task<IEnumerable<string>>> ValidateValue<T>(this IValidator<T> validator, T model, string propertyName)
    {
        return async (obj, property) =>
        {
            var result = await validator.ValidateAsync(ValidationContext<T>.CreateWithOptions(model, x => x.IncludeProperties(propertyName)));
            if (result.IsValid)
                return Array.Empty<string>();
            return result.Errors.Select(e => e.ErrorMessage);
        };
    }
}
