namespace FinanceTracker.Api;

/// <summary>
/// Turns a service's answer into the response. Null or false is the endpoint's status for nothing done, e.g. 409 or
/// 404. True is 204. Anything else is the 200 it already is.
/// </summary>
internal sealed class NoneAnswerFilter : IEndpointFilter
{
    private readonly int _statusCode;

    /// <param name="statusCode">The status for a null or false answer.</param>
    public NoneAnswerFilter(int statusCode) => _statusCode = statusCode;

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        object? answer = await next(context);
        if (answer is null or false)
        {
            return TypedResults.StatusCode(_statusCode);
        }

        if (answer is true)
        {
            return TypedResults.NoContent();
        }

        return answer;
    }
}
