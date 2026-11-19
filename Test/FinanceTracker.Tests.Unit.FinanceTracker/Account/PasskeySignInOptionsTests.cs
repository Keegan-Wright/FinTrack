using FinanceTracker.Components.Account;
using FinanceTracker.Domain.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Account;

/// <summary>
/// The sign in page's passkey options. Passkeys sign in alongside passwords, so they're off when passwords are. With
/// passwords on, they're covered end to end by the integration tests' <c>PasskeyFlowTests</c>.
/// </summary>
public class PasskeySignInOptionsTests
{
    [Test]
    public async Task PasskeyRequestOptions_PasswordSignInOff_AnswersNotFound()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(new SignInMethods { Password = false, SingleSignOn = true });
        builder.Services.AddSingleton(Substitute.For<IUserStore<FinanceTrackerUser>>());
        builder.Services.AddAntiforgery();
        builder.Services.AddAuthentication();
        builder.Services.AddIdentityCore<FinanceTrackerUser>().AddSignInManager();
        await using WebApplication app = builder.Build();
        app.MapAdditionalIdentityEndpoints();
        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(static x => x.RoutePattern.RawText == "/Account/PasskeyRequestOptions");

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        DefaultHttpContext context = new() { RequestServices = scope.ServiceProvider };
        context.Request.Method = HttpMethods.Post;
        context.Response.Body = new MemoryStream();

        // Act
        await endpoint.RequestDelegate!(context);

        // Assert
        await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
    }
}
