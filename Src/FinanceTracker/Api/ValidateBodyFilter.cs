using FluentValidation;
using FluentValidation.Results;

namespace FinanceTracker.Api;

/// <summary>
/// Runs the request body's <see cref="IValidator{T}"/> before the handler and answers with a validation problem
/// (400) when it fails. The pages validate the same rules, this keeps a request that skips them out.
/// </summary>
internal sealed class ValidateBodyFilter<TBody> : IEndpointFilter
{
    private readonly IValidator<TBody> _validator;

    public ValidateBodyFilter(IValidator<TBody> validator) => _validator = validator;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Minimal APIs already reject a missing body, so the argument is always there
        TBody body = context.Arguments.OfType<TBody>().Single();

        ValidationResult result = await _validator.ValidateAsync(body, context.HttpContext.RequestAborted);

        return result.IsValid ? await next(context) : TypedResults.ValidationProblem(result.ToDictionary());
    }
}
