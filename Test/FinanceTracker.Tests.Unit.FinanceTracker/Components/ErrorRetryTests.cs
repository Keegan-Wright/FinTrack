using FinanceTracker.Components.Pages;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Components;

public class ErrorRetryTests
{
    [Test]
    public async Task PathFor_APageThatFailed_IsItsAddressWithItsQuery()
    {
        // Arrange, as the exception handler runs the error page in the failed request
        DefaultHttpContext context = Failed("GET", "/Bills");
        context.Request.Path = "/Error";
        context.Request.QueryString = new QueryString("?month=2026-11");

        // Act
        string? path = ErrorRetry.PathFor(context);

        // Assert
        await Assert.That(path).IsEqualTo("/Bills?month=2026-11");
    }

    [Test]
    [Arguments("POST")]
    [Arguments("PUT")]
    [Arguments("DELETE")]
    public async Task PathFor_ARequestSendingSomething_HasNothingToTryAgain(string method)
    {
        // Arrange, going to its address wouldn't send it again
        DefaultHttpContext context = Failed(method, "/Account/Login");

        // Act
        string? path = ErrorRetry.PathFor(context);

        // Assert
        await Assert.That(path).IsNull();
    }

    [Test]
    public async Task PathFor_TheErrorPageVisitedItself_HasNothingToTryAgain()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Method = "GET";
        context.Request.Path = "/Error";

        // Act
        string? path = ErrorRetry.PathFor(context);

        // Assert
        await Assert.That(path).IsNull();
    }

    private static DefaultHttpContext Failed(string method, string path)
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        IExceptionHandlerPathFeature failed = Substitute.For<IExceptionHandlerPathFeature>();
        failed.Path.Returns(path);
        context.Features.Set(failed);
        return context;
    }
}
