using FinanceTracker.Contracts;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Api;

/// <summary>
/// Answers the failures a request can expect. 404 when the user has nothing by that id, see
/// <see cref="NotFoundException"/>, and 422 with the rule when the domain refuses the change, see
/// <see cref="DomainRuleException"/>, apart from 400 for a body that fails validation.
/// </summary>
public sealed class ExpectedFailureFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (NotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (DomainRuleException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
