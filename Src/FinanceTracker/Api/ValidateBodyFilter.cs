using FluentValidation;
using FluentValidation.Results;

namespace FinanceTracker.Api;

/// <summary>
/// Runs the request body's <see cref="IValidator{T}"/> before the handler and answers with a validation problem
/// (400) when it fails. The pages validate the same rules, this keeps a request that skips them out.
/// </summary>
/// <typeparam name="TBody">The request body's type.</typeparam>
public sealed class ValidateBodyFilter<TBody> : IEndpointFilter
{
    private readonly IValidator<TBody> _validator;

    /// <param name="validator">Checks the body.</param>
    public ValidateBodyFilter(IValidator<TBody> validator) => _validator = validator;

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Since .NET 11 filters run even when the body couldn't be bound, e.g. none was sent. The framework has already
        // answered 400 then, which next passes on
        if (context.Arguments.OfType<TBody>().SingleOrDefault() is not { } body)
        {
            return await next(context);
        }

        ValidationResult result = await _validator.ValidateAsync(body, context.HttpContext.RequestAborted);

        return result.IsValid ? await next(context) : TypedResults.ValidationProblem(result.ToDictionary());
    }
}
