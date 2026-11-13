using FinanceTracker.Api;
using FinanceTracker.Contracts;
using FinanceTracker.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Api;

public class ExpectedFailureFilterTests
{
    [Test]
    public async Task NotFound_Answers404()
    {
        object? result = await InvokeAsync(new NotFoundException("No budget the user can see or change was found."));

        await Assert.That(result).IsTypeOf<NotFound>();
    }

    private const string Rule = "Only its owner can change what a shared goal tracks.";

    [Test]
    public async Task RuleBroken_Answers422WithTheRule()
    {
        object? result = await InvokeAsync(new DomainRuleException(Rule));

        ProblemHttpResult problem = (ProblemHttpResult)result!;
        using (Assert.Multiple())
        {
            await Assert.That(problem.StatusCode).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
            await Assert.That(problem.ProblemDetails.Detail).IsEqualTo(Rule);
        }
    }

    [Test]
    public async Task AnythingElse_IsABug_AndGoesOn()
    {
        await Assert.That(() => InvokeAsync(new InvalidOperationException("Sequence contains no elements")))
            .Throws<InvalidOperationException>();
    }

    private static async Task<object?> InvokeAsync(Exception thrown)
    {
        EndpointFilterInvocationContext context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());
        return await new ExpectedFailureFilter().InvokeAsync(context, _ => throw thrown);
    }
}
