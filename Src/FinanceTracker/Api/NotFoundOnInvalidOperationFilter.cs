namespace FinanceTracker.Api;

/// <summary>
/// Answers 404 for services that report a missing or someone else's record by throwing
/// <see cref="InvalidOperationException"/>. Only for endpoints whose service documents that, elsewhere the exception
/// is a bug and stays a 500.
/// </summary>
internal sealed class NotFoundOnInvalidOperationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
    }
}
