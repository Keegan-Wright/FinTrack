using FinanceTracker.Api;
using FinanceTracker.Contracts.Budgets;
using FinanceTracker.Contracts.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Api;

public class ValidateBodyFilterTests
{
    private static readonly object NextAnswered = new();

    [Test]
    public async Task BodyNotBound_PassesOnTheFrameworksAnswer()
    {
        // Arrange, since .NET 11 the filter runs when no body could be bound, its argument null and a 400 already set
        DefaultHttpContext httpContext = new() { Response = { StatusCode = StatusCodes.Status400BadRequest } };

        // Act
        object? result = await InvokeAsync(httpContext, body: null);

        // Assert
        await Assert.That(result).IsSameReferenceAs(NextAnswered);
    }

    [Test]
    public async Task InvalidBody_AnswersAValidationProblem()
    {
        object? result = await InvokeAsync(new DefaultHttpContext(), new SaveBudget { Name = "Empty" });

        await Assert.That(result).IsTypeOf<ValidationProblem>();
    }

    [Test]
    public async Task ValidBody_GoesOn()
    {
        SaveBudget body = new() { Name = "Groceries", TagIds = [Guid.NewGuid()], MonthlyLimit = 400m };

        object? result = await InvokeAsync(new DefaultHttpContext(), body);

        await Assert.That(result).IsSameReferenceAs(NextAnswered);
    }

    private static async Task<object?> InvokeAsync(HttpContext httpContext, SaveBudget? body)
    {
        EndpointFilterInvocationContext context = new DefaultEndpointFilterInvocationContext(httpContext, body);
        return await new ValidateBodyFilter<SaveBudget>(new SaveBudgetValidator())
            .InvokeAsync(context, _ => ValueTask.FromResult<object?>(NextAnswered));
    }
}
