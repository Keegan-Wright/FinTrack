using FinanceTracker.Contracts;

namespace FinanceTracker.Api;

/// <summary>
/// Answers 409 with the message for services that throw <see cref="NameTakenException"/> when the user already has
/// something with that name.
/// </summary>
internal sealed class ConflictOnNameTakenFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (NameTakenException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
