using FinanceTracker.Contracts.Api;
using FinanceTracker.Tokens;

namespace FinanceTracker.Api;

/// <summary>
/// Rejects API requests missing <see cref="ApiHeaders.Request"/>, which keeps other sites from making them with the
/// user's cookie. See the header for why that's enough. A request with a personal access token doesn't need it, as
/// another site can't send the token.
/// </summary>
internal sealed class ApiRequestHeaderFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpRequest request = context.HttpContext.Request;
        if (request.Headers.ContainsKey(ApiHeaders.Request) || PersonalAccessTokenDefaults.IsTokenRequest(request))
        {
            return next(context);
        }

        return ValueTask.FromResult<object?>(TypedResults.Problem($"The {ApiHeaders.Request} header is required.",
            statusCode: StatusCodes.Status400BadRequest));
    }
}
