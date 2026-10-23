using FinanceTracker.Contracts.Api;

namespace FinanceTracker.Api;

/// <summary>
/// Rejects API requests missing <see cref="ApiHeaders.Request"/>, which keeps other sites from making them with the
/// user's cookie. See the header for why that's enough.
/// </summary>
internal sealed class ApiRequestHeaderFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.Request.Headers.ContainsKey(ApiHeaders.Request)
            ? next(context)
            : ValueTask.FromResult<object?>(TypedResults.Problem($"The {ApiHeaders.Request} header is required.",
                statusCode: StatusCodes.Status400BadRequest));
}
